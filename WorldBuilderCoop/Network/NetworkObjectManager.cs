using ModLoader;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldBuilderCoop.Network
{
    public class NetworkObjectManager
    {
        private Dictionary<int, NetworkObject> _networkObjects = new Dictionary<int, NetworkObject>();
        private Dictionary<int, UserAvatar> _userAvatars = new Dictionary<int, UserAvatar>();
        private Dictionary<string, bool> _selectedByUser = new Dictionary<string, bool>();

        private event Action<NetworkObject> OnObjectRegistered;
        private event Action<int> OnObjectUnregistered;
        private event Action<UserAvatar> OnAvatarRegistered;
        private event Action<int> OnAvatarUnregistered;

        public void RegisterNetworkObject(int networkId, NetworkObject obj)
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
            int networkId = _networkObjects.Count > 0 ? _networkObjects.Keys.Max() + 1 : 1;
            _networkObjects[networkId] = obj;
            obj.NetworkId = networkId;
            OnObjectRegistered?.Invoke(obj);
        }

        public void UnregisterNetworkObject(int networkId)
        {
            if (_networkObjects.Remove(networkId))
                OnObjectUnregistered?.Invoke(networkId);
        }

        public NetworkObject GetNetworkObject(int networkId)
            => _networkObjects.TryGetValue(networkId, out var obj) ? obj : null;

        public bool HasNetworkObject(int networkId) => _networkObjects.ContainsKey(networkId);

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

        public void MarkAsSelectedByUser(int userId, int networkId)
        {
            string key = userId + "_" + networkId;
            _selectedByUser[key] = true;
        }

        public void UnmarkAsSelectedByUser(int userId, int networkId)
        {
            string key = userId + "_" + networkId;
            _selectedByUser.Remove(key);
        }

        public bool IsSelectedByUser(int userId, int networkId)
        {
            string key = userId + "_" + networkId;
            return _selectedByUser.ContainsKey(key) && _selectedByUser[key];
        }

        public void SubscribeToObjectRegistration(Action<NetworkObject> callback) => OnObjectRegistered += callback;
        public void UnsubscribeFromObjectRegistration(Action<NetworkObject> callback) => OnObjectRegistered -= callback;
        public void SubscribeToObjectUnregistration(Action<int> callback) => OnObjectUnregistered += callback;
        public void UnsubscribeFromObjectUnregistration(Action<int> callback) => OnObjectUnregistered -= callback;
        public void SubscribeToAvatarRegistration(Action<UserAvatar> callback) => OnAvatarRegistered += callback;
        public void UnsubscribeFromAvatarRegistration(Action<UserAvatar> callback) => OnAvatarRegistered -= callback;
        public void SubscribeToAvatarUnregistration(Action<int> callback) => OnAvatarUnregistered += callback;
        public void UnsubscribeFromAvatarUnregistration(Action<int> callback) => OnAvatarUnregistered -= callback;
    }
}