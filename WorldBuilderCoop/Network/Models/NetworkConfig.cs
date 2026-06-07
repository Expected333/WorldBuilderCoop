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
        SaveHistory = 13,
        BatchAddToSelection = 14,
        DuplicateSelection = 15,
        StartMapSync = 16,
        EndMapSync = 17,
        UpdateComponent = 18
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
        // ID réseau global 64 bits = (userId << 32) | compteur. Voir NetworkIdAllocator.
        public long NetworkId;
        public string PrefabPath;
        public int PrefabIndex = -1;
    }

    public class ObjectInfo
    {
        public long objectId;
        public int placeIndex;
        public int prefabIndex;
        public byte[] componentData;
    }

    /// <summary>Transform d'un objet identifié, pour la réplication par-objet.</summary>
    public struct NetTransform
    {
        public long objectId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    /// <summary>Mapping explicite source -> nouvel ID pour répliquer une duplication.</summary>
    public struct DuplicateEntry
    {
        public long sourceId;
        public long newId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}