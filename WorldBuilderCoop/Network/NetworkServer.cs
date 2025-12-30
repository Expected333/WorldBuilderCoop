using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    public class NetworkServer
    {
        private TcpListener _tcpListener;
        private List<ConnectedClient> _connectedClients = new List<ConnectedClient>();
        private bool _isConnected;
        private Action<byte[], int> _onPacketReceived;

        public IEnumerable<ConnectedClient> ConnectedClients => _connectedClients;

        public NetworkServer(Action<byte[], int> onPacketReceived)
        {
            _onPacketReceived = onPacketReceived;
        }

        public void Start(int port = NetworkConfig.DefaultPort)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
                _isConnected = true;
                ConsoleBase.WriteLine($"Server started on port {port}");
                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Server start failed: {ex.Message}");
            }
        }

        public IEnumerator ListenLoop()
        {
            while (_isConnected)
            {
                ProcessClients();
                yield return new WaitForSeconds(0.01f);
            }
        }

        private void ProcessClients()
        {
            for (int i = _connectedClients.Count - 1; i >= 0; i--)
            {
                var client = _connectedClients[i];
                if (!client.IsConnected)
                    RemoveClient(client);
                else
                    ReadClientPackets(client);
            }
        }

        private void ReadClientPackets(ConnectedClient client)
        {
            try
            {
                if (client.Client.Available >= NetworkConfig.HeaderSize)
                {
                    byte[] sizeBytes = new byte[4];
                    int readHeader = client.Stream.Read(sizeBytes, 0, 4);
                    if (readHeader < 4) return;

                    int packetSize = BitConverter.ToInt32(sizeBytes, 0);
                    if (packetSize > 0 && packetSize <= NetworkConfig.MaxPacketSize)
                    {
                        byte[] buffer = new byte[packetSize];
                        int totalRead = ReadFullBuffer(client.Stream, buffer, packetSize);

                        if (totalRead == packetSize)
                        {
                            _onPacketReceived?.Invoke(buffer, totalRead);
                            BroadcastToOthers(buffer, packetSize, client.UserId);
                        }
                    }
                }
            }
            catch (Exception)
            {
                RemoveClient(client);
            }
        }

        private int ReadFullBuffer(NetworkStream stream, byte[] buffer, int size)
        {
            int totalRead = 0;
            while (totalRead < size)
            {
                int read = stream.Read(buffer, totalRead, size - totalRead);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }

        private void BroadcastToOthers(byte[] buffer, int size, int senderId)
        {
            byte[] fullPacket = new byte[buffer.Length + 4];
            Buffer.BlockCopy(BitConverter.GetBytes(size), 0, fullPacket, 0, 4);
            Buffer.BlockCopy(buffer, 0, fullPacket, 4, buffer.Length);

            SendToClients(fullPacket, c => c.UserId != senderId);
        }

        private void SendToClients(byte[] packet, Func<ConnectedClient, bool> predicate)
        {
            foreach (var client in _connectedClients)
            {
                if (client.IsConnected && predicate(client))
                {
                    try
                    {
                        client.Stream.Write(packet, 0, packet.Length);
                        client.Stream.Flush();
                    }
                    catch (Exception ex)
                    {
                        ConsoleBase.WriteError($"Send error: {ex.Message}");
                        RemoveClient(client);
                    }
                }
            }
        }

        private void OnClientConnected(IAsyncResult result)
        {
            try
            {
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

                SendAssignIDPacket(connectedClient);
                _tcpListener.BeginAcceptTcpClient(OnClientConnected, null);

                PacketSender.SendLoadMap(WorldBuilderSync.GetMapsObjects(), PacketDistribution.SendToUser, new List<int> { userId });
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"Client connection error: {ex.Message}");
            }
        }

        private void SendAssignIDPacket(ConnectedClient client)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)PacketDistribution.SendToUser);
                    writer.Write((byte)Packets.AssignID);
                    writer.Write(client.UserId);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);

                    client.Stream.Write(packet, 0, packet.Length);
                    client.Stream.Flush();
                }
            }
        }

        private void RemoveClient(ConnectedClient client)
        {
            int userId = client.UserId;
            _connectedClients.Remove(client);
            UserIdManager.ReleaseUserId(userId);
            client.Disconnect();

            PacketSender.SendRemovePlayer(userId, PacketDistribution.SendToOthers);
            WorldBuilderSync.removeUser(userId);
        }

        public void Stop()
        {
            _isConnected = false;
            foreach (var client in _connectedClients)
            {
                client.Disconnect();
                UserIdManager.ReleaseUserId(client.UserId);
            }
            _connectedClients.Clear();
            _tcpListener?.Stop();
        }
    }
}
