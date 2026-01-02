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
        RemovePlayer = 8,
        LoadMapFinished = 9,
        Undo = 10,
        Redo = 11,
        Duplicate = 12,
        SaveHistory = 13
    }

    public enum PacketDistribution
    {
        SendToAll = 1,
        SendToOthers = 2,
        SendToUser = 3,
    }

    public class UserAvatar : MonoBehaviour
    {
        public int UserId;
        public Vector3 position;
        public quaternion rotation;
        public int placeIndex;
    }

    public class NetworkObject : MonoBehaviour
    {
        public int NetworkId;
        public string PrefabPath;
        public int PrefabIndex = -1;
    }

    public class ObjectInfo
    {
        public int objectId;
        public int placeIndex;
        public byte[] componentData;
    }
}