using ModLoader;

namespace WorldBuilderCoop.Network
{
    public static class LocalUserManager
    {
        private static int _localUserId = -1;

        public static int GetLocalUserId()
        {
            if (_localUserId == -1)
            {
                _localUserId = UnityEngine.Random.Range(1000000, 2000000);
                WbLog.Debug($"[LocalUserManager] Assigned local user ID: {_localUserId}");
            }
            return _localUserId;
        }

        public static void ResetUserId()
        {
            _localUserId = -1;
        }
    }
}