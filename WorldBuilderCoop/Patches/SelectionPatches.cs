using HarmonyLib;
using BrokeProtocol.Client.Builder;
using UnityEngine;
using WorldBuilderCoop.Network;
using WorldBuilderCoop.Events;
using ModLoader;
using Steamworks;

namespace WorldBuilderCoop.Patches
{
    [HarmonyPatch(typeof(BlEditorManager), "AddToSelection")]
    public class BlEditorManagerAddToSelection_Patch
    {
        public static bool Prefix(BlEditorManager __instance, Transform t)
        {
            if (WorldBuilderSync.IsRemoteAction) return true;

            if (t == null) return false;
            var networkObj = t.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                if (WorldBuilderSync.blacklistSelection.Contains(networkObj.NetworkId))
                    return false;

                int userId = GetCurrentUserId();
                Core.networkObjectManager.MarkAsSelectedByUser(userId, networkObj.NetworkId);
                WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, true);
            }
            return true;
        }

        private static int GetCurrentUserId()
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsLocalMode())
                return LocalUserManager.GetLocalUserId();
            return SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "RemoveFromSelection")]
    public class BlEditorManagerRemoveFromSelection_Patch
    {
        public static void Postfix(BlEditorManager __instance, Transform t)
        {
            if (WorldBuilderSync.IsRemoteAction) return;

            if (t == null) return;
            var networkObj = t.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                int userId = GetCurrentUserId();
                Core.networkObjectManager.UnmarkAsSelectedByUser(userId, networkObj.NetworkId);
                WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, false);
            }
        }

        private static int GetCurrentUserId()
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsLocalMode())
                return LocalUserManager.GetLocalUserId();
            return SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }
    }
}
