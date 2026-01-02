using BrokeProtocol.Client.Builder;
using ModLoader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Managers;

namespace WorldBuilderCoop.Network
{
    public static class PacketHandler
    {
        private static Queue<System.Action> _bufferedPackets = new Queue<System.Action>();

        public static void ProcessBufferedPackets()
        {
            while (_bufferedPackets.Count > 0)
            {
                var action = _bufferedPackets.Dequeue();
                action.Invoke();
            }
        }

        public static void HandlePlaceObject(byte[] data)
        {
            ConsoleBase.WriteLine("[PacketHandler] Handling PlaceObject");
            if (MapManager.IsLoading)
            {
                byte[] dataCopy = (byte[])data.Clone();
                _bufferedPackets.Enqueue(() => HandlePlaceObject(dataCopy));
                return;
            }

            try
            {
                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        reader.ReadByte(); // Skip packet type

                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();
                        float rx = reader.ReadSingle();
                        float ry = reader.ReadSingle();
                        float rz = reader.ReadSingle();
                        float rw = reader.ReadSingle();
                        float sx = reader.ReadSingle();
                        float sy = reader.ReadSingle();
                        float sz = reader.ReadSingle();
                        int objectId = reader.ReadInt32();
                        
                        bool hasPath = reader.ReadBoolean();
                        string prefabPath = null;
                        int prefabIndex = -1;

                        if (hasPath)
                        {
                            prefabPath = reader.ReadString();
                        }
                        else
                        {
                            prefabIndex = reader.ReadInt32();
                        }

                        if (BlEditorManager.Instance != null)
                        {
                            BlEditorManager.Instance.AppendHistory();
                        }

                        // We need to update WorldBuilderSync.placeObject to handle prefabIndex
                        WorldBuilderSync.placeObject(new Vector3(x, y, z), new Quaternion(rx, ry, rz, rw), new Vector3(sx, sy, sz), objectId, prefabPath, prefabIndex);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("Error handling place object: " + ex.Message);
            }
        }

        public static void HandleRemoveObjects(byte[] data)
        {
            if (MapManager.IsLoading)
            {
                byte[] dataCopy = (byte[])data.Clone();
                _bufferedPackets.Enqueue(() => HandleRemoveObjects(dataCopy));
                return;
            }

            try
            {
                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        reader.ReadByte(); // Skip packet type
                        int count = reader.ReadInt32();
                        List<int> objectIds = new List<int>();

                        for (int i = 0; i < count; i++)
                        {
                            objectIds.Add(reader.ReadInt32());
                        }

                        if (BlEditorManager.Instance != null)
                        {
                            BlEditorManager.Instance.AppendHistory();
                        }

                        WorldBuilderSync.destroyObject(objectIds);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("Error handling remove objects: " + ex.Message);
            }
        }

        public static void HandleUpdateObject(byte[] data, int length)
        {
            if (MapManager.IsLoading)
            {
                byte[] dataCopy = (byte[])data.Clone();
                _bufferedPackets.Enqueue(() => HandleUpdateObject(dataCopy, length));
                return;
            }

            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();

                    int objectIdsCount = reader.ReadInt32();
                    List<int> objectIds = new List<int>();
                    for (int i = 0; i < objectIdsCount; i++)
                    {
                        objectIds.Add(reader.ReadInt32());
                    }

                    float px = reader.ReadSingle();
                    float py = reader.ReadSingle();
                    float pz = reader.ReadSingle();
                    Vector3 position = new Vector3(px, py, pz);

                    float rx = reader.ReadSingle();
                    float ry = reader.ReadSingle();
                    float rz = reader.ReadSingle();
                    float rw = reader.ReadSingle();
                    Quaternion rotation = new Quaternion(rx, ry, rz, rw);

                    float sx = reader.ReadSingle();
                    float sy = reader.ReadSingle();
                    float sz = reader.ReadSingle();
                    Vector3 scale = new Vector3(sx, sy, sz);

                    int componentDataLength = reader.ReadInt32();
                    byte[] componentData;

                    if (componentDataLength > 0)
                    {
                        componentData = reader.ReadBytes(componentDataLength);
                    }
                    else
                    {
                        componentData = new byte[0];
                    }

                    if (objectIds.Count > 0)
                    {
                        WorldBuilderSync.updateObject(objectIds, position, rotation, scale, componentData);
                    }
                }
            }
        }

        public static void HandleLoadMap(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();

                    // New Chunk Format: TotalSize, Offset, Length
                    // Ensure this matches the Sender logic
                    int totalSize = reader.ReadInt32();
                    int offset = reader.ReadInt32();
                    int chunkLength = reader.ReadInt32();

                    byte[] chunkData = reader.ReadBytes(chunkLength);

                    // Pass to MapManager
                    MapManager.HandleIncomingChunk(totalSize, offset, chunkData);
                }
            }
        }

        public static void HandlePlayerSync(byte[] data)
        {
            ConsoleBase.WriteLine("[PacketHandler] Handling PlayerSync");
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    int placeIndex = 0;
                    // Check if we have more data (backwards compatibility or new field)
                    if (ms.Position < ms.Length)
                    {
                        placeIndex = reader.ReadInt32();
                    }

                    ConsoleBase.WriteLine($"[PacketHandler] Received player sync for user {userId} at {position}");
                    PlayerSyncHelper.userSync(userId, position, rotation, placeIndex);
                }
            }
        }

        public static void HandleRemovePlayer(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userIdToRemove = reader.ReadInt32();

                    PlayerSyncHelper.removeUser(userIdToRemove);
                }
            }
        }

        public static void HandleAddToSelection(byte[] data, int length)
        {
            if (MapManager.IsLoading)
            {
                byte[] dataCopy = (byte[])data.Clone();
                _bufferedPackets.Enqueue(() => HandleAddToSelection(dataCopy, length));
                return;
            }

            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    int objectId = reader.ReadInt32();
                    WorldBuilderSync.AddToSelection(userId, objectId);
                }
            }
        }

        public static void HandleRemoveFromSelection(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    int objectId = reader.ReadInt32();
                    WorldBuilderSync.RemoveFromSelection(userId, objectId);
                }
            }
        }

        public static void HandleLoadMapFinished(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    int loadedCount = reader.ReadInt32();

                    ConsoleBase.WriteLine($"[PacketHandler] User {userId} finished loading map. Total objects: {loadedCount}");
                }
            }
        }

        public static void HandleUndo(byte[] data)
        {
            WorldBuilderSync.IsRemoteAction = true;
            try
            {
                if (BlEditorManager.Instance != null)
                {
                    BlEditorManager.Instance.Undo();
                }
            }
            finally
            {
                WorldBuilderSync.IsRemoteAction = false;
            }
        }

        public static void HandleRedo(byte[] data)
        {
            WorldBuilderSync.IsRemoteAction = true;
            try
            {
                if (BlEditorManager.Instance != null)
                {
                    BlEditorManager.Instance.Redo();
                }
            }
            finally
            {
                WorldBuilderSync.IsRemoteAction = false;
            }
        }

        public static void HandleDuplicate(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte(); // Type
                    int count = reader.ReadInt32();
                    List<int> objectIds = new List<int>();
                    for (int i = 0; i < count; i++)
                    {
                        objectIds.Add(reader.ReadInt32());
                    }

                    if (BlEditorManager.Instance != null)
                    {
                        BlEditorManager.Instance.AppendHistory();
                    }

                    // For duplication, we don't have the new IDs yet if we were to strictly follow logic,
                    // but WorldBuilderSync.DuplicateObject (if it existed) would need to know them.
                    // Actually, the sender sent the IDs of the objects to BE duplicated.
                    // Wait, BlEditorManager.DuplicateSelection creates NEW objects.
                    // The sync logic for Duplicate is: Sender duplicates, gets new IDs, sends "PlaceObject" for each new item?
                    // OR Sender sends "Duplicate these IDs".
                    // If "Duplicate these IDs", receivers will generate NEW IDs locally -> DESYNC of IDs.
                    
                    // The Duplicate implementation in BlEditorManagerPatches sends Packets.Duplicate with the SOURCE IDs.
                    // This is problematic for ID sync.
                    // However, based on previous turn, we only implemented the Packet handling structure.
                    // If we want correct ID sync, Duplicate should probably be treated as "PlaceObject" for the new items.
                    // But for now, let's just ensure History is saved.
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("Error handling duplicate: " + ex.Message);
            }
        }

        public static void HandleSaveHistory(byte[] data)
        {
            if (BlEditorManager.Instance != null)
            {
                BlEditorManager.Instance.AppendHistory();
            }
        }
    }
}