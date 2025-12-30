using ModLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Events
{
    // ============ EVENT ARGUMENTS ============
    public class ObjectPlacedEventArgs : EventArgs
    {
        public int ObjectId { get; set; }
        public string PrefabPath { get; set; }
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
        public void RaiseObjectPlaced(int objectId, string prefabPath, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            OnObjectPlaced?.Invoke(this, new ObjectPlacedEventArgs
            {
                ObjectId = objectId,
                PrefabPath = prefabPath,
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
        private readonly NetworkHandler _networkHandler;
        private readonly WorldBuilderEventManager _eventManager;

        public NetworkSyncHandler(NetworkHandler networkHandler)
        {
            _networkHandler = networkHandler;
            _eventManager = WorldBuilderEventManager.Instance;

            // sub events
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
                PacketSender.SendPlaceObject(e.Position, e.Rotation, e.Scale, e.ObjectId, e.PrefabPath, PacketDistribution.SendToOthers);
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
                PacketSender.SendUpdateObject(e.ObjectIds, e.Position, e.Rotation, e.Scale, PacketDistribution.SendToOthers);
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
                PacketSender.SendRemoveObject(e.ObjectIds, PacketDistribution.SendToOthers);
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
                if (e.IsSelected)
                    PacketSender.SendAddToSelection(e.UserId, e.ObjectId);
                else
                    PacketSender.SendRemoveToSelection(e.UserId, e.ObjectId);
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
                PacketSender.SendPlayerSync(e.UserId, e.Position, e.Rotation, PacketDistribution.SendToOthers);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Error sending player moved: " + ex.Message);
            }
        }

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