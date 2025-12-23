using BrokeProtocol.Client.Builder;
using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using WorldBuilderCoop.Network;
namespace WorldBuilderCoop
{
    public class NetworkHandler
    {
        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isHost;
        private bool _isConnected;
        private int _userId;
        public List<ConnectedClient> _connectedClients = new List<ConnectedClient>();

        private int _myUserId = -1;
        public bool IsHost => _isHost;
        public bool IsConnected => _isConnected;
        public int MyUserId => _myUserId;

        public class ConnectedClient
        {
            public int UserId { get; set; }
            public TcpClient Client { get; set; }
            public NetworkStream Stream { get; set; }
            public string IpAddress { get; set; }
        }

        public void CreateHost(int port = 7777)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
                _isHost = true;
                _isConnected = true;
                _userId = 1;
                _myUserId = 0;

                ConsoleBase.WriteLine($"Host created on port {port}");
                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
                BlEditorManager.Instance.StartCoroutine(listenHostPacketLoop());
                BlEditorManager.Instance.StartCoroutine(WorldBuilderSync.listenPlayerMovementLoop());
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Failed to create host: {ex.Message}");
            }
        }

        public void JoinAsClient(string ipAddress, int port = 7777)
        {
            try
            {
                if (_isConnected)
                    Shutdown();
                _tcpClient = new TcpClient();
                _tcpClient.BeginConnect(ipAddress, port, OnConnectedToHost, null);
                ConsoleBase.WriteLine($"Connecting to {ipAddress}:{port}");
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Failed to join: {ex.Message}");
            }
        }

        // ======== LISTENER PART ========

        public IEnumerator listenHostPacketLoop()
        {
            while (_isConnected && _isHost)
            {
                try
                {
                    List<ConnectedClient> disconnectedClients = new List<ConnectedClient>();

                    foreach (var client in _connectedClients)
                    {
                        if (client.Client.Connected && client.Stream.DataAvailable)
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead = client.Stream.Read(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                                ProcessPacket(buffer, bytesRead);
                        }
                        else if (!client.Client.Connected)
                            disconnectedClients.Add(client);
                    }

                    foreach (var client in disconnectedClients)
                    {
                        _connectedClients.Remove(client);
                        ConsoleBase.WriteLine($"Client disconnected: {client.IpAddress} (ID: {client.UserId})");
                    }
                }
                catch (Exception ex)
                {
                    ConsoleBase.WriteError($"Host listen error: {ex.Message}");
                }
                yield return new WaitForSeconds(0.01f);
            }
            yield break;
        }

        public IEnumerator listenPacketLoop()
        {
            yield return new WaitForSeconds(0.5f);

            while (_isConnected)
            {
                try
                {
                    if (_tcpClient != null && _tcpClient.Connected)
                    {
                        if (_networkStream == null)
                            _networkStream = _tcpClient.GetStream();

                        if (_networkStream.DataAvailable)
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead = _networkStream.Read(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                            {
                                ProcessPacket(buffer, bytesRead);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleBase.WriteError($"Listen error: {ex.Message}");
                }
                yield return new WaitForSeconds(0.01f);
            }
            yield break;
        }

        private void ProcessPacket(byte[] data, int length)
        {
            if (length < 2) return;

            PacketDistribution distribution = (PacketDistribution)data[0];
            Packets packetType = (Packets)data[1];

            switch (packetType)
            {
                case Packets.PlaceObject:
                    PacketRecieve.HandlePlaceObject(data, length);
                    break;
                case Packets.RemoveObject:
                    PacketRecieve.HandleRemoveObject(data, length);
                    break;
                case Packets.UpdateObject:
                    PacketRecieve.HandleUpdateObject(data, length);
                    break;
                case Packets.LoadMap:
                    PacketRecieve.HandleLoadMap(data, length);
                    break;
                case Packets.PlayerSync:
                    PacketRecieve.HandlePlayerSync(data, length);
                    break;
            }
        }

        // ======== SEND PACKETS PART ========

        public void SendPacket(byte[] packet, PacketDistribution distribution, List<int> userIds = null)
        {
            try
            {
                if (_isHost)
                {
                    switch (distribution)
                    {
                        case PacketDistribution.SendToAll:
                            foreach (var client in _connectedClients)
                            {
                                if (client.Client.Connected)
                                {
                                    client.Stream.Write(packet, 0, packet.Length);
                                    client.Stream.Flush();
                                }
                            }
                            break;
                        case PacketDistribution.SendToOthers:
                            foreach (var client in _connectedClients)
                            {
                                if (client.Client.Connected && (userIds == null || !userIds.Contains(client.UserId)))
                                {
                                    client.Stream.Write(packet, 0, packet.Length);
                                    client.Stream.Flush();
                                }
                            }
                            break;
                        case PacketDistribution.SendToUser:
                            if (userIds != null)
                            {
                                foreach (var userId in userIds)
                                {
                                    var targetClient = _connectedClients.Find(c => c.UserId == userId);
                                    if (targetClient != null && targetClient.Client.Connected)
                                    {
                                        targetClient.Stream.Write(packet, 0, packet.Length);
                                        targetClient.Stream.Flush();
                                    }
                                }
                            }
                            break;
                    }
                }
                else
                {
                    if (_tcpClient != null && _tcpClient.Connected)
                    {
                        _networkStream = _tcpClient.GetStream();
                        _networkStream.Write(packet, 0, packet.Length);
                        _networkStream.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Send error: {ex.Message}");
            }
        }

        // ======== EVENT PART ========

        private void OnClientConnected(IAsyncResult result)
        {
            try
            {
                if (result == null) return;
                TcpClient client = _tcpListener.EndAcceptTcpClient(result);
                var connectedClient = new ConnectedClient
                {
                    UserId = _userId++,
                    Client = client,
                    Stream = client.GetStream(),
                    IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()
                };
                _connectedClients.Add(connectedClient);
                ConsoleBase.WriteLine($"Client connected: {connectedClient.IpAddress} (ID: {connectedClient.UserId})");

                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Client connection error: {ex.Message}");
            }
        }

        private void OnConnectedToHost(IAsyncResult result)
        {
            try
            {
                if (result == null) return;
                _tcpClient.EndConnect(result);
                _isConnected = true;
                _myUserId = UserIdManager.GetNextUserId();

                if (_myUserId == -1)
                {
                    ConsoleBase.WriteError("No available user IDs");
                    return;
                }

                ConsoleBase.WriteLine($"Connected to host with ID: {_myUserId}");
                WorldBuilderSync.addUser(_myUserId, Vector3.zero, Quaternion.identity);
                PacketSender.SendPlayerSync(_myUserId, Vector3.zero, Quaternion.identity, PacketDistribution.SendToOthers);
                BlEditorManager.Instance.StartCoroutine(listenPacketLoop());
                BlEditorManager.Instance.StartCoroutine(WorldBuilderSync.listenPlayerMovementLoop());
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Connection failed: {ex.Message}");
                _isConnected = false;
            }
        }

        public void Disconnect()
        {
            if (_isHost)
            {
                foreach (var client in _connectedClients)
                {
                    try
                    {
                        WorldBuilderSync.removeUser(client.UserId);
                        UserIdManager.ReleaseUserId(client.UserId);
                        client.Stream?.Close();
                        client.Client?.Close();
                    }
                    catch { }
                }
                _connectedClients.Clear();
                _tcpListener?.Stop();
            }
            else
            {
                WorldBuilderSync.removeUser(MyUserId);
                UserIdManager.ReleaseUserId(MyUserId);
                _tcpClient?.Close();
                _networkStream?.Close();
            }

            _isConnected = false;
            _isHost = false;
        }

        public void Shutdown()
        {
            _tcpClient?.Close();
            _tcpListener?.Stop();
            foreach (var client in _connectedClients)
            {
                client.Stream?.Close();
                client.Client?.Close();
            }
            _connectedClients.Clear();
            _isConnected = false;
            _isHost = false;
            ConsoleBase.WriteLine("Network shutdown");
        }
    }

    public class NetworkObject : MonoBehaviour
    {
        public int NetworkId { get; set; }
    }

    public class UserAvatar : MonoBehaviour
    {
        public int UserId { get; set; }
    }

    public enum Packets
    {
        PlaceObject = 1,
        RemoveObject = 2,
        UpdateObject = 3,
        LoadMap = 4,
        PlayerSync = 6,
    }

    public enum PacketDistribution
    {
        SendToAll = 1,
        SendToOthers = 2,
        SendToUser = 3,
    }
}