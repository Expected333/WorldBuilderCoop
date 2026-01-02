using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using HarmonyLib;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Events;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Patches
{
    [HarmonyPatch(typeof(BrokeProtocol.Client.UI.BlPrefabItemButton), "Clicked")]
    public class BlPrefabItemButton_Patch
    {
        public static bool Prefix(BrokeProtocol.Client.UI.BlPrefabItemButton __instance)
        {
            GameObject asset = Traverse.Create(__instance).Field("asset").GetValue() as GameObject;

            if (__instance.CompareTag("Folder"))
            {
                MonoBehaviourSingleton<BlEditorManager>.Instance.FillBrowser(Path.Combine(MonoBehaviourSingleton<BlEditorManager>.Instance.currentPrefabPath, __instance.name), search: false);
                return false;
            }
            if (MonoBehaviourSingleton<BlEditorManager>.Instance.objectInspector is BlPrefabObjectInspector)
            {
                MonoBehaviourSingleton<BlEditorManager>.Instance.objectInspector.OnValueChange(asset);
                MonoBehaviourSingleton<BlEditorManager>.Instance.objectInspector = null;
                return false;
            }
            if ((bool)MonoBehaviourSingleton<BlEditorManager>.Instance.itemOptionInspector)
            {
                MonoBehaviourSingleton<BlEditorManager>.Instance.itemOptionInspector.OnValueChange(asset.name);
                MonoBehaviourSingleton<BlEditorManager>.Instance.itemOptionInspector = null;
                return false;
            }

            Transform mTransform = MonoBehaviourSingleton<BlSceneCamera>.Instance.mTransform;
            Ray ray = new Ray(mTransform.position, mTransform.forward);
            Transform obj;
            Vector3 hitPoint;
            Vector3 position = ((!MonoBehaviourSingleton<BlEditorManager>.Instance.ObjectRaycast(ray, out obj, out hitPoint)) ? MonoBehaviourSingleton<BlSceneCamera>.Instance.RoundedPivot : hitPoint.Snap(0.01f));
            GameObject gameObject = MonoBehaviourSingleton<SceneManager>.Instance.InstantiateEditor(asset, MonoBehaviourSingleton<SceneManager>.Instance.currentPlace, position, Quaternion.identity);

            if (gameObject != null && gameObject.transform != null)
            {
                NetworkObject networkObject = gameObject.AddComponent<NetworkObject>();
                networkObject.NetworkId = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

                string prefabName = __instance.name;
                string currentPath = MonoBehaviourSingleton<BlEditorManager>.Instance.currentPrefabPath;
                string fullPath = Path.Combine(currentPath, prefabName);

                MonoBehaviourSingleton<BlEditorManager>.Instance.SetSelection(gameObject.transform);

                WorldBuilderEventManager.Instance.RaiseObjectPlaced(networkObject.NetworkId, fullPath, position, Quaternion.identity, Vector3.one);
            }

            return false;
        }
    }
}
