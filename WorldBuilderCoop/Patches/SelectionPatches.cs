using HarmonyLib;
using BrokeProtocol.Client.Builder;
using UnityEngine;
using WorldBuilderCoop.Network;
using WorldBuilderCoop.Events;
using ModLoader;
using Steamworks;
using WorldBuilderCoop.Managers;

namespace WorldBuilderCoop.Patches
{
    [HarmonyPatch(typeof(BlEditorManager), "AddToSelection")]
    public class BlEditorManagerAddToSelection_Patch
    {
        public static bool Prefix(BlEditorManager __instance, Transform t)
        {
            if (WorldBuilderSync.IsRemoteAction) return true;
            // Note : on ne bloque PAS pendant un envoi de map à un arrivant (IsSyncing).
            // Le snapshot est figé avant l'envoi ; l'host peut continuer à éditer, ses actions
            // sont mises en file et rejouées chez l'arrivant après chargement.

            if (t == null) return false;
            var networkObj = t.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                int userId = GetCurrentUserId();

                // Check if selected by another user
                if (Core.networkObjectManager.IsSelectedByAnyOtherUser(userId, networkObj.NetworkId))
                {
                    return false; // Block selection
                }

                if (WorldBuilderSync.blacklistSelection.Contains(networkObj.NetworkId))
                    return false;

                // Use Batcher
                if (SelectionBatcher.Instance != null)
                {
                    SelectionBatcher.Instance.RequestSelection(networkObj.NetworkId);
                }
                else
                {
                    // Fallback if batcher missing (shouldn't happen)
                    Core.networkObjectManager.MarkAsSelectedByUser(userId, networkObj.NetworkId);
                    WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, true);
                }
            }
            return true;
        }

        private static int GetCurrentUserId() => NetworkIdentity.GetUserId();
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

        private static int GetCurrentUserId() => NetworkIdentity.GetUserId();
    }

    // ClearSelection ne passe PAS par RemoveFromSelection (il vide la liste directement).
    // Sans ce patch, désélectionner via un clic dans le vide ou un changement de sélection
    // ne relâche jamais le verrou réseau → les autres joueurs ne peuvent plus prendre l'objet.
    // On relâche donc ici chaque objet sélectionné (déverrouillage local + broadcast) avant le vidage.
    [HarmonyPatch(typeof(BlEditorManager), "ClearSelection")]
    public class BlEditorManagerClearSelection_Patch
    {
        public static void Prefix(BlEditorManager __instance)
        {
            if (WorldBuilderSync.IsRemoteAction) return;
            if (SteamNetworkManager.Instance == null) return;
            if (__instance == null || __instance.selectedTransforms == null) return;

            int userId = NetworkIdentity.GetUserId();
            foreach (var t in __instance.selectedTransforms)
            {
                if (t == null) continue;
                var networkObj = t.GetComponent<NetworkObject>();
                if (networkObj == null) continue;

                // Annule un éventuel verrou encore en attente d'envoi (même frame).
                SelectionBatcher.Instance?.CancelPendingSelection(networkObj.NetworkId);
                Core.networkObjectManager.UnmarkAsSelectedByUser(userId, networkObj.NetworkId);
                WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, false);
            }
        }
    }
}
