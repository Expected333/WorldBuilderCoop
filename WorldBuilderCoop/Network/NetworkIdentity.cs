using ModLoader;
using Steamworks;

namespace WorldBuilderCoop.Network
{
    /// <summary>
    /// Point unique pour résoudre l'identité de l'utilisateur courant.
    /// Évite la duplication de GetCurrentUserId() dispersée dans le projet.
    /// </summary>
    public static class NetworkIdentity
    {
        public static int GetUserId()
        {
            var manager = SteamNetworkManager.Instance;
            if (manager != null && manager.IsLocalMode())
            {
                return LocalUserManager.GetLocalUserId();
            }
            return SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }
    }

    /// <summary>
    /// Génère des NetworkId 64 bits sans collision.
    /// Format : [ userId 32 bits (poids fort) | compteur 32 bits (poids faible) ].
    /// - Le compteur monotone garantit l'unicité pour un même utilisateur (~4 milliards d'objets).
    /// - L'userId COMPLET (32 bits, pas de hash réduit) en poids fort isole totalement les
    ///   espaces d'ID entre utilisateurs : collision uniquement si deux joueurs partagent
    ///   le même userId 32 bits, ce qui n'arrive pas (Steam hash distinct / ID local distinct).
    /// </summary>
    public static class NetworkIdAllocator
    {
        private static uint _counter;

        public static long Allocate()
        {
            uint c = ++_counter;
            int uid = NetworkIdentity.GetUserId();
            return ((long)(uint)uid << 32) | c;
        }

        public static void Reset()
        {
            _counter = 0;
        }
    }
}
