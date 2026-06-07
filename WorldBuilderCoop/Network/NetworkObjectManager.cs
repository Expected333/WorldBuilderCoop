using ModLoader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldBuilderCoop.Network
{
    public class NetworkObjectManager
    {
        private Dictionary<long, NetworkObject> _networkObjects = new Dictionary<long, NetworkObject>();
        private Dictionary<int, UserAvatar> _userAvatars = new Dictionary<int, UserAvatar>();
        private Dictionary<string, bool> _selectedByUser = new Dictionary<string, bool>();

        private event Action<NetworkObject> OnObjectRegistered;
        private event Action<long> OnObjectUnregistered;
        private event Action<UserAvatar> OnAvatarRegistered;
        private event Action<int> OnAvatarUnregistered;

        public void RegisterNetworkObject(long networkId, NetworkObject obj)
        {
            if (_networkObjects.ContainsKey(networkId))
            {
                ConsoleBase.WriteError("NetworkObject " + networkId + " already registered");
                return;
            }
            _networkObjects[networkId] = obj;
            obj.NetworkId = networkId;
            OnObjectRegistered?.Invoke(obj);
        }

        public void AddNetworkObject(NetworkObject obj)
        {
            long networkId = NetworkIdAllocator.Allocate();
            _networkObjects[networkId] = obj;
            obj.NetworkId = networkId;
            OnObjectRegistered?.Invoke(obj);
        }

        public void UnregisterNetworkObject(long networkId)
        {
            if (_networkObjects.Remove(networkId))
                OnObjectUnregistered?.Invoke(networkId);
        }

        public NetworkObject GetNetworkObject(long networkId)
            => _networkObjects.TryGetValue(networkId, out var obj) ? obj : null;

        public bool HasNetworkObject(long networkId) => _networkObjects.ContainsKey(networkId);

        public IEnumerable<NetworkObject> GetAllNetworkObjects() => _networkObjects.Values;

        public void RegisterUserAvatar(int userId, UserAvatar avatar)
        {
            if (_userAvatars.ContainsKey(userId))
            {
                ConsoleBase.WriteError("UserAvatar " + userId + " already registered");
                return;
            }
            _userAvatars[userId] = avatar;
            avatar.UserId = userId;
            OnAvatarRegistered?.Invoke(avatar);
        }

        public void UnregisterUserAvatar(int userId)
        {
            if (_userAvatars.Remove(userId))
                OnAvatarUnregistered?.Invoke(userId);
        }

        public UserAvatar GetUserAvatar(int userId)
            => _userAvatars.TryGetValue(userId, out var avatar) ? avatar : null;

        public bool HasUserAvatar(int userId) => _userAvatars.ContainsKey(userId);

        public IEnumerable<UserAvatar> GetAllUserAvatars() => _userAvatars.Values;

        public void ClearAll()
        {
            _networkObjects.Clear();
            _userAvatars.Clear();
            _selectedByUser.Clear();
        }

        /// <summary>Vide les objets/sélections mais conserve les avatars (rechargement de map).</summary>
        public void ClearObjects()
        {
            _networkObjects.Clear();
            _selectedByUser.Clear();
        }

        public void MarkAsSelectedByUser(int userId, long networkId)
        {
            string key = userId + "_" + networkId;
            _selectedByUser[key] = true;
        }

        public void UnmarkAsSelectedByUser(int userId, long networkId)
        {
            string key = userId + "_" + networkId;
            _selectedByUser.Remove(key);
        }

        public bool IsSelectedByUser(int userId, long networkId)
        {
            string key = userId + "_" + networkId;
            return _selectedByUser.ContainsKey(key) && _selectedByUser[key];
        }

        public bool IsSelectedByAnyOtherUser(int currentUserId, long networkId)
        {
            foreach (var kvp in _selectedByUser)
            {
                if (kvp.Value)
                {
                    string[] parts = kvp.Key.Split('_');
                    if (parts.Length == 2)
                    {
                        int userId = int.Parse(parts[0]);
                        long objId = long.Parse(parts[1]);

                        if (objId == networkId && userId != currentUserId)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>Relâche tous les verrous de sélection d'un joueur (ex. à sa déconnexion),
        /// sinon ses objets restent "verrouillés" pour les autres indéfiniment.</summary>
        public void ReleaseAllByUser(int userId)
        {
            string prefix = userId + "_";
            List<string> toRemove = null;
            foreach (var key in _selectedByUser.Keys)
            {
                if (key.StartsWith(prefix))
                {
                    if (toRemove == null) toRemove = new List<string>();
                    toRemove.Add(key);
                }
            }
            if (toRemove != null)
            {
                foreach (var key in toRemove) _selectedByUser.Remove(key);
            }
        }

        public List<long> GetSelectedObjectsByUser(int userId)
        {
            List<long> selectedIds = new List<long>();
            string prefix = userId + "_";
            foreach (var kvp in _selectedByUser)
            {
                if (kvp.Value && kvp.Key.StartsWith(prefix))
                {
                    string[] parts = kvp.Key.Split('_');
                    if (parts.Length == 2 && long.TryParse(parts[1], out long objId))
                    {
                        selectedIds.Add(objId);
                    }
                }
            }
            return selectedIds;
        }

        public void SubscribeToObjectRegistration(Action<NetworkObject> callback) => OnObjectRegistered += callback;
        public void UnsubscribeFromObjectRegistration(Action<NetworkObject> callback) => OnObjectRegistered -= callback;
        public void SubscribeToObjectUnregistration(Action<long> callback) => OnObjectUnregistered += callback;
        public void UnsubscribeFromObjectUnregistration(Action<long> callback) => OnObjectUnregistered -= callback;
        public void SubscribeToAvatarRegistration(Action<UserAvatar> callback) => OnAvatarRegistered += callback;
        public void UnsubscribeFromAvatarRegistration(Action<UserAvatar> callback) => OnAvatarRegistered -= callback;
        public void SubscribeToAvatarUnregistration(Action<int> callback) => OnAvatarUnregistered += callback;
        public void UnsubscribeFromAvatarUnregistration(Action<int> callback) => OnAvatarUnregistered -= callback;
    }
}