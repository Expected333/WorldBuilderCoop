using BrokeProtocol.Client.Builder;
using HarmonyLib;
using UnityEngine;
using WorldBuilderCoop.Managers;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Patches
{
    /// <summary>
    /// Réplique les éditions de propriétés faites via l'inspecteur.
    /// BlTypeInspector.SetValue est le point central appelé à chaque changement de champ
    /// (il déclenche aussi AppendHistory). On marque l'objet actif comme "à resynchroniser" :
    /// le SelectionBatcher enverra son état complet (transform + champs custom) une fois par frame.
    /// </summary>
    [HarmonyPatch(typeof(BlTypeInspector), "SetValue")]
    public class BlTypeInspectorSetValue_Patch
    {
        public static void Postfix()
        {
            if (WorldBuilderSync.IsRemoteAction) return;
            if (SteamNetworkManager.Instance == null || !SteamNetworkManager.Instance.IsConnected) return;
            if (SelectionBatcher.Instance == null) return;

            BlEditorManager editor = BlEditorManager.Instance;
            if (editor == null) return;

            Transform active = editor.ActiveTransform;
            if (active == null) return;

            NetworkObject netObj = active.GetComponent<NetworkObject>();
            if (netObj == null) return;

            SelectionBatcher.Instance.RequestComponentSync(netObj.NetworkId);
        }
    }
}
