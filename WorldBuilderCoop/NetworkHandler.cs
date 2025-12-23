using BrokeProtocol.Client.Builder;
using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
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
        private List<ConnectedClient> _connectedClients = new List<ConnectedClient>();

        public bool IsHost => _isHost;
        public bool IsConnected => _isConnected;

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
                ConsoleBase.WriteLine($"Host created on port {port}");
                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
                BlEditorManager.Instance.StartCoroutine(listenHostPacketLoop());
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
                {
                    Shutdown();
                }
                _tcpClient = new TcpClient();
                _tcpClient.BeginConnect(ipAddress, port, OnConnectedToHost, null);
                ConsoleBase.WriteLine($"Connecting to {ipAddress}:{port}");
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Failed to join: {ex.Message}");
            }
        }

        public IEnumerator listenHostPacketLoop()
        {
            while (_isConnected && _isHost)
            {
                try
                {
                    foreach (var client in _connectedClients)
                    {
                        if (client.Client.Connected && client.Stream.DataAvailable)
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead = client.Stream.Read(buffer, 0, buffer.Length);

                            if (bytesRead > 0)
                            {
                                ProcessPacket(buffer, bytesRead);
                            }
                        }
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
                        {
                            _networkStream = _tcpClient.GetStream();
                        }

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
                    HandlePlaceObject(data, length);
                    break;
                case Packets.RemoveObject:
                    HandleRemoveObject(data, length);
                    break;
                case Packets.UpdateObject:
                    HandleUpdateObject(data, length);
                    break;
                case Packets.LoadMap:
                    HandleLoadMap(data, length);
                    break;
                case Packets.PlayerSync:
                    HandlePlayerSync(data, length);
                    break;
            }
        }

        private void HandlePlaceObject(byte[] data, int length)
        {
            int offset = 2;
            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );
            offset += 16;

            Vector3 scale = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            int objectId = BitConverter.ToInt32(data, offset);
            offset += 4;

            int prefabNameLength = BitConverter.ToInt32(data, offset);
            offset += 4;

            string prefabName = System.Text.Encoding.UTF8.GetString(data, offset, prefabNameLength);

            WorldBuilderSync.placeObject(position, rotation, scale, objectId, prefabName);
        }

        private void HandleRemoveObject(byte[] data, int length)
        {
            int offset = 2;
            int count = BitConverter.ToInt32(data, offset);
            offset += 4;

            List<int> objectIds = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int objectId = BitConverter.ToInt32(data, offset);
                objectIds.Add(objectId);
                offset += 4;
            }

            WorldBuilderSync.destroyObject(objectIds);
        }

        private void HandleUpdateObject(byte[] data, int length)
        {
            int offset = 2;
            int objectId = BitConverter.ToInt32(data, offset);
            offset += 4;

            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );
            offset += 16;

            Vector3 scale = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            int componentDataLength = BitConverter.ToInt32(data, offset);
            offset += 4;

            byte[] componentData = componentDataLength > 0 ? new byte[componentDataLength] : null;
            if (componentDataLength > 0)
            {
                Buffer.BlockCopy(data, offset, componentData, 0, componentDataLength);
            }

            WorldBuilderSync.updateObject(objectId, position, rotation, scale, componentData);
        }

        private void HandleLoadMap(byte[] data, int length)
        {
            int nameLength = BitConverter.ToInt32(data, 2);
            string mapName = System.Text.Encoding.UTF8.GetString(data, 6, nameLength);
            WorldBuilderSync.loadMap(mapName);
        }

        private void HandlePlayerSync(byte[] data, int length)
        {
            int offset = 2;
            int userId = BitConverter.ToInt32(data, offset);
            offset += 4;

            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );

            WorldBuilderSync.userSync(userId, position, rotation);
        }

        public void SendPlaceObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabName, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] prefabNameBytes = System.Text.Encoding.UTF8.GetBytes(prefabName);
            byte[] packet = new byte[2 + 12 + 16 + 12 + 4 + 4 + prefabNameBytes.Length];

            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.PlaceObject;

            int offset = 2;
            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, offset + 12, 4);
            offset += 16;

            Buffer.BlockCopy(BitConverter.GetBytes(scale.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(prefabNameBytes.Length), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(prefabNameBytes, 0, packet, offset, prefabNameBytes.Length);

            SendPacket(packet, distribution, userIds);
        }

        public void SendRemoveObject(List<int> objectIds, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] packet = new byte[2 + 4 + (objectIds.Count * 4)];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.RemoveObject;

            int offset = 2;
            Buffer.BlockCopy(BitConverter.GetBytes(objectIds.Count), 0, packet, offset, 4);
            offset += 4;

            foreach (var objectId in objectIds)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
                offset += 4;
            }

            SendPacket(packet, distribution, userIds);
        }

        public void SendUpdateObject(int objectId, Vector3 position, Quaternion rotation, Vector3 scale, PacketDistribution distribution = PacketDistribution.SendToAll, byte[] componentData = null, List<int> userIds = null)
        {
            int componentDataLength = componentData != null ? componentData.Length : 0;
            byte[] packet = new byte[2 + 4 + 12 + 16 + 12 + 4 + componentDataLength];

            int offset = 0;
            packet[offset++] = (byte)distribution;
            packet[offset++] = (byte)Packets.UpdateObject;

            Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, offset + 12, 4);
            offset += 16;

            Buffer.BlockCopy(BitConverter.GetBytes(scale.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(componentDataLength), 0, packet, offset, 4);
            offset += 4;

            if (componentDataLength > 0)
            {
                Buffer.BlockCopy(componentData, 0, packet, offset, componentDataLength);
            }

            SendPacket(packet, distribution, userIds);
        }

        public void SendLoadMap(string mapName, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(mapName);
            byte[] packet = new byte[2 + 4 + nameBytes.Length];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.LoadMap;
            Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, packet, 2, 4);
            Buffer.BlockCopy(nameBytes, 0, packet, 6, nameBytes.Length);
            SendPacket(packet, distribution, userIds);
        }

        public void SendPlayerSync(int userId, Vector3 position, Quaternion rotation, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] packet = new byte[2 + 4 + 12 + 16];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.PlayerSync;
            Buffer.BlockCopy(BitConverter.GetBytes(userId), 0, packet, 2, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, 6, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, 10, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, 14, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, 18, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, 22, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, 26, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, 30, 4);
            SendPacket(packet, distribution, userIds);
        }

        private void SendPacket(byte[] packet, PacketDistribution distribution, List<int> userIds = null)
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
                ConsoleBase.WriteLine("Connected to host");
                BlEditorManager.Instance.StartCoroutine(listenPacketLoop());
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Connection failed: {ex.Message}");
                _isConnected = false;
            }
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