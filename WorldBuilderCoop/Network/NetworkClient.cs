using ModLoader;
using System;
using System.Net.Sockets;

namespace WorldBuilderCoop.Network
{
    public class NetworkClient
    {
        public TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public NetworkStream Stream => _networkStream;

        public void Connect(string ipAddress, int port, Action onSuccess, Action<string> onError)
        {
            try
            {
                _tcpClient = new TcpClient();
                _tcpClient.BeginConnect(ipAddress, port, result =>
                {
                    try
                    {
                        _tcpClient.EndConnect(result);
                        _isConnected = true;
                        _networkStream = _tcpClient.GetStream();
                        onSuccess?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        _isConnected = false;
                        onError?.Invoke(ex.Message);
                    }
                }, null);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }

        public void SendPacket(byte[] packet)
        {
            if (_isConnected && _networkStream != null)
            {
                try
                {
                    _networkStream.Write(packet, 0, packet.Length);
                    _networkStream.Flush();
                }
                catch (Exception ex)
                {
                    ConsoleBase.WriteError($"Send error: {ex.Message}");
                }
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _networkStream?.Close();
            _tcpClient?.Close();
        }
    }
}