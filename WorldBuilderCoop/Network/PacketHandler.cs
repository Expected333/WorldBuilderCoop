using BrokeProtocol.Client.Builder;
using ModLoader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Events;
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
            WbLog.Debug("[PacketHandler] Handling PlaceObject");
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
                        long objectId = reader.ReadInt64();

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
                        List<long> objectIds = new List<long>();

                        for (int i = 0; i < count; i++)
                        {
                            objectIds.Add(reader.ReadInt64());
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

            try
            {
                using (var ms = new MemoryStream(data))
                {
                    using (var reader = new BinaryReader(ms))
                    {
                        reader.ReadByte(); // PacketType

                        int count = reader.ReadInt32();
                        var transforms = new List<NetTransform>(count);
                        for (int i = 0; i < count; i++)
                        {
                            var t = new NetTransform
                            {
                                objectId = reader.ReadInt64(),
                                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                                rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                                scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
                            };
                            transforms.Add(t);
                        }

                        WorldBuilderSync.updateObject(transforms);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("Error handling update object: " + ex.Message);
            }
        }

        public static void HandleUpdateComponent(byte[] data)
        {
            if (MapManager.IsLoading)
            {
                byte[] dataCopy = (byte[])data.Clone();
                _bufferedPackets.Enqueue(() => HandleUpdateComponent(dataCopy));
                return;
            }

            try
            {
                using (var ms = new MemoryStream(data))
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte(); // Packet type
                    long objectId = reader.ReadInt64();
                    string json = reader.ReadString();

                    NetworkObject netObj = Core.networkObjectManager.GetNetworkObject(objectId);
                    if (netObj == null || netObj.transform == null)
                    {
                        ConsoleBase.WriteError($"[PacketHandler] UpdateComponent: object {objectId} introuvable");
                        return;
                    }

                    WorldBuilderSync.IsRemoteAction = true;
                    try
                    {
                        MapManager.ApplyObjectParametersJson(netObj.transform, json);
                    }
                    finally
                    {
                        WorldBuilderSync.IsRemoteAction = false;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("Error handling update component: " + ex.Message);
            }
        }

        public static void HandleLoadMap(byte[] data, int length)
        {
            // Already handled by SteamNetworkManager queueing system, but if called directly:
            WbLog.Debug("[PacketHandler] HandleLoadMap called (chunk processing)");
            
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Skip packet type
                int totalSize = reader.ReadInt32();
                int totalChunks = reader.ReadInt32();
                int chunkIndex = reader.ReadInt32();
                int offset = reader.ReadInt32();
                int chunkSize = reader.ReadInt32();
                byte[] chunkData = reader.ReadBytes(chunkSize);

                MapManager.HandleIncomingChunk(totalSize, totalChunks, chunkIndex, offset, chunkData);
            }
        }

        public static void HandleAddToSelection(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte(); // Type
                    int userId = reader.ReadInt32();
                    long objectId = reader.ReadInt64();

                    WorldBuilderSync.AddToSelection(userId, objectId);
                    Core.networkObjectManager.MarkAsSelectedByUser(userId, objectId);
                    // Pas de RaiseSelectionChanged : on applique localement, on ne re-broadcast pas (écho).
                }
            }
        }

        public static void HandleRemoveFromSelection(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte(); // Type
                    int userId = reader.ReadInt32();
                    long objectId = reader.ReadInt64();

                    WorldBuilderSync.RemoveFromSelection(userId, objectId);
                    Core.networkObjectManager.UnmarkAsSelectedByUser(userId, objectId);
                    // Pas de RaiseSelectionChanged : application locale uniquement (pas d'écho).
                }
            }
        }

        public static void HandlePlayerSync(byte[] data)
        {
            // WbLog.Debug("[PacketHandler] Handling PlayerSync");
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    int placeIndex = 0;
                    if (ms.Position < ms.Length)
                    {
                        placeIndex = reader.ReadInt32();
                    }

                    // WbLog.Debug($"[PacketHandler] Received player sync for user {userId} at {position}");
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
                    reader.ReadByte();
                    int userId = reader.ReadInt32();
                    PlayerSyncHelper.RemoveUser(userId);
                }
            }
        }

        public static void HandleLoadMapFinished(byte[] data)
        {
            WbLog.Debug("[PacketHandler] Map load finished signal received.");
            MapManager.FinishLoading();
        }

        public static void HandleUndo(byte[] data)
        {
            if (BlEditorManager.Instance != null)
            {
                WorldBuilderSync.IsRemoteAction = true;
                BlEditorManager.Instance.Undo();
                WorldBuilderSync.IsRemoteAction = false;
            }
        }

        public static void HandleRedo(byte[] data)
        {
            if (BlEditorManager.Instance != null)
            {
                WorldBuilderSync.IsRemoteAction = true;
                BlEditorManager.Instance.Redo();
                WorldBuilderSync.IsRemoteAction = false;
            }
        }

        public static void HandleSaveHistory(byte[] data)
        {
            if (BlEditorManager.Instance != null)
            {
                BlEditorManager.Instance.AppendHistory();
            }
        }

        public static void HandleDuplicate(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte();
                long sourceId = reader.ReadInt64();
                long newId = reader.ReadInt64();
                Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Quaternion rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                WorldBuilderSync.DuplicateObject(sourceId, newId, pos, rot, scale);
            }
        }

        public static void HandleStartMapSync(byte[] data)
        {
            // Reverted: MapManager.SetSyncState(true);
        }

        public static void HandleEndMapSync(byte[] data)
        {
            // Reverted: MapManager.SetSyncState(false);
        }

        public static void HandleBatchAddToSelection(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Packet type
                int userId = reader.ReadInt32();
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    long objectId = reader.ReadInt64();
                    WorldBuilderSync.AddToSelection(userId, objectId);
                    Core.networkObjectManager.MarkAsSelectedByUser(userId, objectId);
                    // Pas de RaiseSelectionChanged : application locale uniquement (pas d'écho).
                }
            }
        }

        public static void HandleDuplicateSelection(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadByte(); // Packet type
                reader.ReadInt32(); // userId (non utilisé : le mapping est explicite)
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    long sourceId = reader.ReadInt64();
                    long newId = reader.ReadInt64();
                    Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rot = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    // Réutilise la logique commune (recherche source, instanciation, enregistrement).
                    WorldBuilderSync.DuplicateObject(sourceId, newId, pos, rot, scale);
                }
            }
        }
    }
}