using BrokeProtocol.Client.Builder;
using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Unity.Mathematics;
using UnityEngine;
using WorldBuilderCoop.Network;
namespace WorldBuilderCoop
{
    public class NetworkHandler
    {
        public const int TargetFPS = 30;
        public const float SyncInterval = 1f / TargetFPS;

        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isHost;
        private bool _isConnected;
        private int _userId;
        public List<ConnectedClient> _connectedClients = new List<ConnectedClient>();

        public List<UserAvatar> connectedClientAvatar = new List<UserAvatar>();

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
                for (int i = _connectedClients.Count - 1; i >= 0; i--)
                {
                    var client = _connectedClients[i];
                    try
                    {
                        if (client.Client.Connected)
                        {
                            if (client.Client.Available >= 4)
                            {
                                byte[] sizeBytes = new byte[4];
                                int readHeader = client.Stream.Read(sizeBytes, 0, 4);
                                if (readHeader < 4) continue;

                                int packetSize = BitConverter.ToInt32(sizeBytes, 0);

                                if (packetSize > 0 && packetSize <= 1048576)
                                {
                                    byte[] buffer = new byte[packetSize];
                                    int totalRead = 0;
                                    while (totalRead < packetSize)
                                    {
                                        int read = client.Stream.Read(buffer, totalRead, packetSize - totalRead);
                                        if (read == 0) break;
                                        totalRead += read;
                                    }

                                    ProcessPacket(buffer, totalRead);

                                    byte[] fullPacket = new byte[buffer.Length + 4];
                                    Buffer.BlockCopy(BitConverter.GetBytes(packetSize), 0, fullPacket, 0, 4);
                                    Buffer.BlockCopy(buffer, 0, fullPacket, 4, buffer.Length);

                                    SendPacket(fullPacket, PacketDistribution.SendToOthers, new List<int> { client.UserId });
                                }
                            }
                        }
                        else
                        {
                            HandleDisconnect(client);
                        }
                    }
                    catch (Exception)
                    {
                        HandleDisconnect(client);
                    }
                }
                yield return new WaitForSeconds(0.01f);
            }
        }

        private void HandleDisconnect(ConnectedClient client)
        {
            int disconnectedId = client.UserId;

            _connectedClients.Remove(client);
            UserIdManager.ReleaseUserId(disconnectedId);

            try { client.Stream?.Close(); } catch { }
            try { client.Client?.Close(); } catch { }

            PacketSender.SendRemovePlayer(disconnectedId, PacketDistribution.SendToOthers);
            WorldBuilderSync.removeUser(disconnectedId);
        }

        public IEnumerator listenPacketLoop()
        {
            yield return new WaitForSeconds(0.5f);

            while (_isConnected)
            {
                bool hasError = false;

                if (_tcpClient != null && _tcpClient.Connected)
                {
                    if (_networkStream == null)
                    {
                        try { _networkStream = _tcpClient.GetStream(); }
                        catch { hasError = true; }
                    }

                    if (!hasError && _tcpClient.Available >= 4)
                    {
                        int expectedPacketSize = 0;
                        try
                        {
                            byte[] sizeBytes = new byte[4];
                            _networkStream.Read(sizeBytes, 0, 4);
                            expectedPacketSize = BitConverter.ToInt32(sizeBytes, 0);
                        }
                        catch (Exception ex)
                        {
                            ConsoleBase.WriteError($"Read size error: {ex.Message}");
                            hasError = true;
                        }

                        if (!hasError && expectedPacketSize > 0 && expectedPacketSize <= 1048576)
                        {
                            float startTime = Time.time;
                            while (_tcpClient.Available < expectedPacketSize && _isConnected)
                            {
                                if (Time.time - startTime > 5f) break;
                                yield return null;
                            }

                            if (_tcpClient.Available >= expectedPacketSize)
                            {
                                byte[] packetBuffer = new byte[expectedPacketSize];
                                try
                                {
                                    int totalRead = 0;
                                    while (totalRead < expectedPacketSize)
                                    {
                                        int read = _networkStream.Read(packetBuffer, totalRead, expectedPacketSize - totalRead);
                                        totalRead += read;
                                    }
                                    ProcessPacket(packetBuffer, totalRead);
                                }
                                catch (Exception ex)
                                {
                                    ConsoleBase.WriteError($"Process packet error: {ex.Message}");
                                }
                            }
                        }
                    }
                }

                yield return new WaitForSeconds(SyncInterval);
            }
        }

        private void ProcessPacket(byte[] data, int length)
        {
            if (length < 2) return;

            PacketDistribution distribution = (PacketDistribution)data[0];
            Packets packetType = (Packets)data[1];

            switch (packetType)
            {
                case Packets.AssignID:
                    PacketRecieve.HandleAssignID(data);
                    break;

                case Packets.PlaceObject:
                    PacketRecieve.HandlePlaceObject(data, length);
                    break;

                case Packets.RemoveObjects:
                    PacketRecieve.HandleRemoveObject(data, length);
                    break;

                case Packets.UpdateObjects:
                    PacketRecieve.HandleUpdateObject(data, length);
                    break;

                case Packets.LoadMap:
                    PacketRecieve.HandleLoadMap(data, length);
                    break;

                case Packets.PlayerSync:
                    PacketRecieve.HandlePlayerSync(data, length);
                    break;

                case Packets.SelectObjects:
                    break;

                case Packets.DeselectObjects:
                    break;
                case Packets.RemovePlayer:
                    PacketRecieve.HandleRemovePlayer(data);
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
                    foreach (var client in _connectedClients)
                    {
                        if (client.Client.Connected)
                        {
                            bool shouldSend = distribution == PacketDistribution.SendToAll ||
                                             (distribution == PacketDistribution.SendToOthers && (userIds == null || !userIds.Contains(client.UserId))) ||
                                             (distribution == PacketDistribution.SendToUser && userIds != null && userIds.Contains(client.UserId));

                            if (shouldSend)
                            {
                                client.Stream.Write(packet, 0, packet.Length);
                                client.Stream.Flush();
                            }
                        }
                    }
                }
                else
                {
                    if (_tcpClient != null && _tcpClient.Connected && _networkStream != null)
                    {
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

        // host recieved connected client
        private void OnClientConnected(IAsyncResult result)
        {
            try
            {
                if (result == null) return;
                TcpClient client = _tcpListener.EndAcceptTcpClient(result);
                int userId = UserIdManager.GetNextUserId();

                var connectedClient = new ConnectedClient
                {
                    UserId = userId,
                    Client = client,
                    Stream = client.GetStream(),
                    IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString()
                };
                _connectedClients.Add(connectedClient);

                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write(0);
                        writer.Write((byte)PacketDistribution.SendToUser);
                        writer.Write((byte)Packets.AssignID);
                        writer.Write(userId);

                        byte[] packet = ms.ToArray();
                        int dataLength = packet.Length - 4;
                        Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);

                        connectedClient.Stream.Write(packet, 0, packet.Length);
                        connectedClient.Stream.Flush();
                    }
                }

                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);

                PacketSender.SendLoadMap(WorldBuilderSync.getMapsObjects(), PacketDistribution.SendToUser, new List<int>() { userId });
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"{ex.Message}");
            }
        }

        //client just being connect
        private void OnConnectedToHost(IAsyncResult result)
        {
            try
            {
                if (result == null) return;
                _tcpClient.EndConnect(result);
                _isConnected = true;
                _networkStream = _tcpClient.GetStream();

                ConsoleBase.WriteLine("Connected to host. Waiting for assignment.");
                BlEditorManager.Instance.StartCoroutine(listenPacketLoop());
                BlEditorManager.Instance.StartCoroutine(WorldBuilderSync.listenPlayerMovementLoop());
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Connection failed: {ex.Message}");
                _isConnected = false;
            }
        }

        public void SetMyUserId(int id)
        {
            _myUserId = id;
            ConsoleBase.WriteLine($"Local UserID set to: {_myUserId}");
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
        public void Start()
        {
            NetworkObjectManager.Register(this.NetworkId, this);
        }
    }

    public class UserAvatar : MonoBehaviour
    {
        public int UserId { get; set; }
        public Vector3 position { get; set; }
        public quaternion rotation { get; set; }
        public int placeIndex { get; set; }
    }

    public class ObjectInfo
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public int objectId;
        public int prefabIndex;
        public int placeIndex;
    }

    public enum Packets
    {
        AssignID = 0,
        PlaceObject = 1,
        RemoveObjects = 2,
        UpdateObjects = 3,
        LoadMap = 4,
        SelectObjects = 5,
        PlayerSync = 6,
        DeselectObjects = 7,
        RemovePlayer = 8
    }

    public enum PacketDistribution
    {
        SendToAll = 1,
        SendToOthers = 2,
        SendToUser = 3,
    }

    public static class NetworkObjectManager
    {
        private static Dictionary<int, NetworkObject> _cachedObjects = new Dictionary<int, NetworkObject>();

        public static void Register(int id, NetworkObject obj)
        {
            if (!_cachedObjects.ContainsKey(id)) _cachedObjects.Add(id, obj);
        }

        public static void Unregister(int id)
        {
            if (_cachedObjects.ContainsKey(id)) _cachedObjects.Remove(id);
        }

        public static NetworkObject GetNetworkObject(int id)
        {
            if (_cachedObjects.TryGetValue(id, out NetworkObject obj))
            {
                if (obj != null) return obj;
                _cachedObjects.Remove(id);
            }
            return null;
        }

        public static void Clear() => _cachedObjects.Clear();
    }
}