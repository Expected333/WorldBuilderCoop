using ModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Events
{
    // ============ EVENT ARGUMENTS ============
    public class ObjectPlacedEventArgs : EventArgs
    {
        public int ObjectId { get; set; }
        public string PrefabPath { get; set; }
        public int PrefabIndex { get; set; } = -1;
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
    }

    public class ObjectMovedEventArgs : EventArgs
    {
        public List<int> ObjectIds { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
    }

    public class ObjectRemovedEventArgs : EventArgs
    {
        public List<int> ObjectIds { get; set; }
    }

    public class SelectionChangedEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public int ObjectId { get; set; }
        public bool IsSelected { get; set; }
    }

    public class PlayerMovedEventArgs : EventArgs
    {
        public int UserId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
    }

    // ============ EVENT MANAGER ============
    public class WorldBuilderEventManager
    {
        // event sync
        public event EventHandler<ObjectPlacedEventArgs> OnObjectPlaced;
        public event EventHandler<ObjectMovedEventArgs> OnObjectMoved;
        public event EventHandler<ObjectRemovedEventArgs> OnObjectRemoved;
        public event EventHandler<SelectionChangedEventArgs> OnSelectionChanged;
        public event EventHandler<PlayerMovedEventArgs> OnPlayerMoved;

        // connection events
        public event EventHandler<EventArgs> OnConnectedToHost;
        public event EventHandler<EventArgs> OnDisconnected;
        public event EventHandler<EventArgs> OnHostStarted;

        // Singleton
        private static WorldBuilderEventManager _instance;
        public static WorldBuilderEventManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new WorldBuilderEventManager();
                return _instance;
            }
        }

        // ============ INVOKEURS ============
        public void RaiseObjectPlaced(int objectId, string prefabPath, Vector3 position, Quaternion rotation, Vector3 scale, int prefabIndex = -1)
        {
            OnObjectPlaced?.Invoke(this, new ObjectPlacedEventArgs
            {
                ObjectId = objectId,
                PrefabPath = prefabPath,
                PrefabIndex = prefabIndex,
                Position = position,
                Rotation = rotation,
                Scale = scale
            });
        }

        public void RaiseObjectMoved(List<int> objectIds, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            OnObjectMoved?.Invoke(this, new ObjectMovedEventArgs
            {
                ObjectIds = objectIds,
                Position = position,
                Rotation = rotation,
                Scale = scale
            });
        }

        public void RaiseObjectRemoved(List<int> objectIds)
        {
            OnObjectRemoved?.Invoke(this, new ObjectRemovedEventArgs
            {
                ObjectIds = objectIds
            });
        }

        public void RaiseSelectionChanged(int userId, int objectId, bool isSelected)
        {
            OnSelectionChanged?.Invoke(this, new SelectionChangedEventArgs
            {
                UserId = userId,
                ObjectId = objectId,
                IsSelected = isSelected
            });
        }

        public void RaisePlayerMoved(int userId, Vector3 position, Quaternion rotation)
        {
            OnPlayerMoved?.Invoke(this, new PlayerMovedEventArgs
            {
                UserId = userId,
                Position = position,
                Rotation = rotation
            });
        }

        public void RaiseConnectedToHost()
        {
            OnConnectedToHost?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseDisconnected()
        {
            OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseHostStarted()
        {
            OnHostStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    // ============ NETWORK SYNC HANDLER ============
    public class NetworkSyncHandler
    {
        private readonly WorldBuilderEventManager _eventManager;

        public NetworkSyncHandler()
        {
            _eventManager = WorldBuilderEventManager.Instance;

            // Subscribe to events
            _eventManager.OnObjectPlaced += HandleObjectPlaced;
            _eventManager.OnObjectMoved += HandleObjectMoved;
            _eventManager.OnObjectRemoved += HandleObjectRemoved;
            _eventManager.OnSelectionChanged += HandleSelectionChanged;
            _eventManager.OnPlayerMoved += HandlePlayerMoved;
        }

        private void HandleObjectPlaced(object sender, ObjectPlacedEventArgs e)
        {
            try
            {
                byte[] data = SerializePlaceObject(e.Position, e.Rotation, e.Scale, e.ObjectId, e.PrefabPath, e.PrefabIndex);
                SendPacket(data);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending object placed: " + ex.Message);
            }
        }

        private void HandleObjectMoved(object sender, ObjectMovedEventArgs e)
        {
            try
            {
                byte[] data = SerializeUpdateObject(e.ObjectIds, e.Position, e.Rotation, e.Scale);
                SendPacket(data);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending object moved: " + ex.Message);
            }
        }

        private void HandleObjectRemoved(object sender, ObjectRemovedEventArgs e)
        {
            try
            {
                byte[] data = SerializeRemoveObject(e.ObjectIds);
                SendPacket(data);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending object removed: " + ex.Message);
            }
        }

        private void HandleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                byte[] data;
                if (e.IsSelected)
                    data = SerializeAddToSelection(e.UserId, e.ObjectId);
                else
                    data = SerializeRemoveFromSelection(e.UserId, e.ObjectId);

                SendPacket(data);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending selection changed: " + ex.Message);
            }
        }

        private void HandlePlayerMoved(object sender, PlayerMovedEventArgs e)
        {
            try
            {
                byte[] data = SerializePlayerSync(e.UserId, e.Position, e.Rotation);
                SendPacket(data);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending player moved: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════
        // SEND HELPER - Host or Client
        // ═══════════════════════════════════════════

        private void SendPacket(byte[] data)
        {
            if (SteamNetworkManager.Instance == null)
            {
                ConsoleBase.WriteError("SteamNetworkManager not initialized");
                return;
            }

            SteamNetworkManager.Instance.SendToAll(data);
        }

        // ═══════════════════════════════════════════
        // SERIALIZATION METHODS
        // ═══════════════════════════════════════════

        private byte[] SerializePlaceObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabPath, int prefabIndex)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.PlaceObject);
                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);
                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);
                    writer.Write(scale.x);
                    writer.Write(scale.y);
                    writer.Write(scale.z);
                    writer.Write(objectId);
                    
                    bool hasPath = !string.IsNullOrEmpty(prefabPath);
                    writer.Write(hasPath);
                    if (hasPath)
                    {
                        writer.Write(prefabPath);
                    }
                    else
                    {
                        writer.Write(prefabIndex);
                    }

                    return ms.ToArray();
                }
            }
        }

        private byte[] SerializeRemoveObject(List<int> objectIds)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.RemoveObjects);
                    writer.Write(objectIds.Count);

                    foreach (var objectId in objectIds)
                    {
                        writer.Write(objectId);
                    }

                    return ms.ToArray();
                }
            }
        }

        private byte[] SerializeUpdateObject(List<int> objectIds, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.UpdateObjects);

                    if (objectIds != null)
                    {
                        writer.Write(objectIds.Count);
                        foreach (int id in objectIds)
                        {
                            writer.Write(id);
                        }
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);

                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);

                    writer.Write(scale.x);
                    writer.Write(scale.y);
                    writer.Write(scale.z);

                    // No component data for now
                    writer.Write(0);

                    return ms.ToArray();
                }
            }
        }

        private byte[] SerializePlayerSync(int userId, Vector3 position, Quaternion rotation)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.PlayerSync);
                    writer.Write(userId);
                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);
                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);

                    return ms.ToArray();
                }
            }
        }

        private byte[] SerializeAddToSelection(int userId, int objectId)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.AddToSelection);
                    writer.Write(userId);
                    writer.Write(objectId);

                    return ms.ToArray();
                }
            }
        }

        private byte[] SerializeRemoveFromSelection(int userId, int objectId)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.RemoveFromSelection);
                    writer.Write(userId);
                    writer.Write(objectId);

                    return ms.ToArray();
                }
            }
        }

        // ═══════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════

        public void Cleanup()
        {
            _eventManager.OnObjectPlaced -= HandleObjectPlaced;
            _eventManager.OnObjectMoved -= HandleObjectMoved;
            _eventManager.OnObjectRemoved -= HandleObjectRemoved;
            _eventManager.OnSelectionChanged -= HandleSelectionChanged;
            _eventManager.OnPlayerMoved -= HandlePlayerMoved;
        }
    }
}