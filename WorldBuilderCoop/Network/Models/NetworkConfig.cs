using Unity.Mathematics;
using UnityEngine;

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

    public enum Packets
    {
        AssignID = 0,
        PlaceObject = 1,
        RemoveObjects = 2,
        UpdateObjects = 3,
        LoadMap = 4,
        AddToSelection = 5,
        PlayerSync = 6,
        RemoveFromSelection = 7,
        RemovePlayer = 8
    }

    public enum PacketDistribution
    {
        SendToAll = 1,
        SendToOthers = 2,
        SendToUser = 3,
    }

    public class UserAvatar : MonoBehaviour
    {
        public int UserId { get; set; }
        public Vector3 position { get; set; }
        public quaternion rotation { get; set; }
        public int placeIndex { get; set; }
    }

    public class NetworkObject : MonoBehaviour
    {
        public int NetworkId { get; set; }
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
}