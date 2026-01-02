using BrokeProtocol.Managers;
using HarmonyLib;
using ModLoader;
using System.Collections;
using UnityEngine;
using WorldBuilderCoop.Managers;

namespace WorldBuilderCoop.Patches
{
    [HarmonyPatch(typeof(BrokeProtocol.Client.Builder.BlEditorManager), "LoadMap")]
    public class BlEditorManagerLoadMap_Patch
    {
        public static bool WaitingForProcessMap = false;

        public static bool Prefix()
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected)
            {
                if (!SteamNetworkManager.Instance.IsHost)
                {
                    ConsoleBase.WriteError("[WorldBuilder] Only the HOST can load maps!");
                    return false;
                }
            }
            return true;
        }

        public static void Postfix(BrokeProtocol.Client.UI.BlPrefabItemButton __instance)
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected && SteamNetworkManager.Instance.IsHost)
            {
                WaitingForProcessMap = true;
                ConsoleBase.WriteLine("[Host] Map load initiated. Waiting for ProcessMap to finalize...");
            }
        }
    }

    [HarmonyPatch(typeof(SceneManager), "ProcessMap")]
    public class SceneManagerProcessMap_Patch
    {
        public static void Postfix()
        {
            if (BlEditorManagerLoadMap_Patch.WaitingForProcessMap)
            {
                BlEditorManagerLoadMap_Patch.WaitingForProcessMap = false;
                if (SteamNetworkManager.Instance != null)
                {
                    SteamNetworkManager.Instance.StartCoroutine(SerializeAndSendLoadMap());
                }
            }
        }

        private static IEnumerator SerializeAndSendLoadMap()
        {
            yield return null;

            ConsoleBase.WriteLine("[Host] ProcessMap finished. Serializing map...");
            var dataToSend = MapManager.SerializeLevel(false);

            ConsoleBase.WriteLine("[Host] Sending map to clients...");
            yield return MapManager.SendMapToClients(dataToSend);
            ConsoleBase.WriteLine("[Host] Map Sync Completed.");
        }
    }
}
