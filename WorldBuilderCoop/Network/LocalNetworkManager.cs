using ModLoader;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace WorldBuilderCoop.Network
{
    public class LocalNetworkManager
    {
        private TcpListener _tcpListener;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private List<TcpClient> _connectedClients = new List<TcpClient>();
        private const int LocalPort = 7777;

        public bool IsConnected { get; private set; }
        public bool IsHost { get; private set; }

        public bool TryStartHost(Action<TcpClient> onClientConnected)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Loopback, LocalPort);
                _tcpListener.Start();
                IsHost = true;
                IsConnected = true;

                ConsoleBase.WriteLine($"[LocalNetwork] Host listening on localhost:{LocalPort}");
                _tcpListener.BeginAcceptTcpClient(result => OnClientConnected(result, onClientConnected), null);
                return true;
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Host bind failed, will try as client: {ex.Message}");
                return false;
            }
        }

        public void StartClient(Action onSuccess, Action<string> onError)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.BeginConnect("127.0.0.1", LocalPort, result => OnConnectedToHost(result, onSuccess, onError), null);
                ConsoleBase.WriteLine($"[LocalNetwork] Attempting to connect to localhost:{LocalPort}");
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        private void OnClientConnected(IAsyncResult result, Action<TcpClient> onClientConnected)
        {
            try
            {
                TcpClient client = _tcpListener.EndAcceptTcpClient(result);
                ConsoleBase.WriteLine($"[LocalNetwork] Client connected from {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                onClientConnected?.Invoke(client);
                _tcpListener.BeginAcceptTcpClient(r => OnClientConnected(r, onClientConnected), null);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Accept error: {ex.Message}");
            }
        }

        private void OnConnectedToHost(IAsyncResult result, Action onSuccess, Action<string> onError)
        {
            try
            {
                _tcpClient.EndConnect(result);
                _networkStream = _tcpClient.GetStream();
                IsConnected = true;
                ConsoleBase.WriteLine("[LocalNetwork] Connected to host");
                onSuccess?.Invoke();
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Connection failed: {ex.Message}");
                onError?.Invoke(ex.Message);
            }
        }

        public void ListenToClient(TcpClient client, Action<byte[]> onPacketReceived)
        {
            _connectedClients.Add(client);
            var stream = client.GetStream();
            ConsoleBase.WriteLine("[LocalNetwork] Started listening to client");

            try
            {
                while (client.Connected && IsHost)
                {
                    if (client.Available >= 4)
                    {
                        ConsoleBase.WriteLine("[LocalNetwork] Data available on client socket");
                        byte[] sizeBytes = new byte[4];
                        if (stream.Read(sizeBytes, 0, 4) < 4) break;

                        int packetSize = BitConverter.ToInt32(sizeBytes, 0);
                        ConsoleBase.WriteLine($"[LocalNetwork] Received packet size: {packetSize}");

                        if (packetSize <= 0 || packetSize > 1048576) continue;

                        byte[] buffer = new byte[packetSize];
                        int totalRead = ReadFullBuffer(stream, buffer, packetSize);

                        if (totalRead == packetSize)
                        {
                            ConsoleBase.WriteLine($"[LocalNetwork] Packet received and applied - Type: {buffer[0]}");
                            onPacketReceived?.Invoke(buffer);
                            BroadcastToOthers(buffer, client);
                        }
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Client listen error: {ex.Message}");
            }

            ConsoleBase.WriteLine("[LocalNetwork] Stopped listening to client");
            _connectedClients.Remove(client);
            stream?.Close();
            client?.Close();
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

        private void BroadcastToOthers(byte[] data, TcpClient sender)
        {
            foreach (var client in _connectedClients)
            {
                if (client != sender && client.Connected)
                {
                    try
                    {
                        SendPacket(data, client.GetStream());
                    }
                    catch (Exception ex)
                    {
                        ConsoleBase.WriteError($"[LocalNetwork] Broadcast error: {ex.Message}");
                    }
                }
            }
        }

        public void SendToAll(byte[] data, bool isHost)
        {
            ConsoleBase.WriteLine($"[LocalNetwork] SendToAll called - IsHost: {isHost}, DataSize: {data.Length}");

            if (isHost)
            {
                ConsoleBase.WriteLine($"[LocalNetwork] Broadcasting to {_connectedClients.Count} clients");
                foreach (var client in _connectedClients)
                {
                    try
                    {
                        SendPacket(data, client.GetStream());
                    }
                    catch (Exception ex)
                    {
                        ConsoleBase.WriteError($"[LocalNetwork] Broadcast error: {ex.Message}");
                    }
                }
            }
            else
            {
                ConsoleBase.WriteLine("[LocalNetwork] Sending to host");
                SendPacket(data);
            }
        }

        public void SendToHost(byte[] data)
        {
            ConsoleBase.WriteLine($"[LocalNetwork] SendToHost called - DataSize: {data.Length}");
            SendPacket(data);
        }

        public void SendPacket(byte[] data, NetworkStream stream = null)
        {
            var targetStream = stream ?? _networkStream;
            if (targetStream == null || !targetStream.CanWrite)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Cannot send - stream is null or not writable");
                return;
            }

            try
            {
                byte[] sizeBytes = BitConverter.GetBytes(data.Length);
                targetStream.Write(sizeBytes, 0, 4);
                targetStream.Write(data, 0, data.Length);
                targetStream.Flush();
                ConsoleBase.WriteLine($"[LocalNetwork] Packet sent successfully - Size: {data.Length}");
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Send error: {ex.Message}");
            }
        }

        public byte[] ReceivePacket()
        {
            if (_networkStream == null || !_networkStream.CanRead)
                return null;

            try
            {
                if (_tcpClient.Available >= 4)
                {
                    byte[] sizeBytes = new byte[4];
                    if (_networkStream.Read(sizeBytes, 0, 4) < 4)
                        return null;

                    int packetSize = BitConverter.ToInt32(sizeBytes, 0);
                    ConsoleBase.WriteLine($"[LocalNetwork] Client received packet size: {packetSize}");

                    if (packetSize <= 0 || packetSize > 1048576)
                        return null;

                    byte[] buffer = new byte[packetSize];
                    int totalRead = ReadFullBuffer(_networkStream, buffer, packetSize);

                    if (totalRead == packetSize)
                    {
                        ConsoleBase.WriteLine($"[LocalNetwork] Client received packet - Type: {buffer[0]}");
                        return buffer;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError($"[LocalNetwork] Client receive error: {ex.Message}");
            }

            return null;
        }

        public void Disconnect()
        {
            IsConnected = false;
            _networkStream?.Close();
            _tcpClient?.Close();
            _tcpListener?.Stop();

            foreach (var client in _connectedClients)
            {
                try { client?.Close(); } catch { }
            }
            _connectedClients.Clear();
        }
    }
}