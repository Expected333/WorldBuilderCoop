namespace WorldBuilderCoop.Network
{
    public static class NetworkConfig
    {
        public const int TargetFPS = 60;
        public const float SyncInterval = 1f / TargetFPS;
        public const int HeaderSize = 4;
        public const int MaxPacketSize = 1048576;
        public const float PacketTimeout = 5f;
        public const int DefaultPort = 7777;
    }
}