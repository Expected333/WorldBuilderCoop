using System.Collections.Generic;

namespace WorldBuilderCoop.Network
{
    public static class UserIdManager
    {
        private static HashSet<int> _usedIds = new HashSet<int>();
        private const int MIN_ID = 1;
        private const int MAX_ID = 9;

        public static int GetNextUserId()
        {
            for (int i = MIN_ID; i <= MAX_ID; i++)
            {
                if (!_usedIds.Contains(i))
                {
                    _usedIds.Add(i);
                    return i;
                }
            }
            return -1;
        }

        public static void ReleaseUserId(int userId)
        {
            _usedIds.Remove(userId);
        }

        public static bool IsIdAvailable(int userId)
        {
            return !_usedIds.Contains(userId);
        }

        public static void Reset()
        {
            _usedIds.Clear();
        }
    }
}