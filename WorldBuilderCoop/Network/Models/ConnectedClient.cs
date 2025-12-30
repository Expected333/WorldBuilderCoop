using System.Net.Sockets;

namespace WorldBuilderCoop.Network
{
    public class ConnectedClient
    {
        public int UserId { get; set; }
        public TcpClient Client { get; set; }
        public NetworkStream Stream { get; set; }
        public string IpAddress { get; set; }

        public bool IsConnected => Client?.Connected ?? false;

        public void Disconnect()
        {
            try { Stream?.Close(); } catch { }
            try { Client?.Close(); } catch { }
        }
    }
}