using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using HarmonyLib;
using ModLoader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    internal class Patches
    {
        [HarmonyPatch(typeof(BrokeProtocol.Client.Builder.BlEditorManager), "Start")]
        public class BlEditorManager_Patch
        {
            public static void Postfix(BlEditorManager __instance)
            {
                // bcs i got some shit, need to delay
                if (__instance != null)
                {
                    ConsoleBase.WriteLine(__instance);
                    __instance.StartCoroutine(ApplyBuilderThemeDelayed(__instance));
                }
            }

            private static System.Collections.IEnumerator ApplyBuilderThemeDelayed(BlEditorManager instance)
            {
                // Wait one frame
                yield return new WaitForFixedUpdate();

                try
                {
                    UIHosting.ApplyBlEditorTheme();
                }
                catch (System.Exception ex)
                {
                    ConsoleBase.WriteLine($"[WorldBuilder] WB_Patch delayed error: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(BrokeProtocol.Client.Builder.BlEditorManager), "DeleteSelection")]
        public class BlEditorManagerDeleteSelection_Patch
        {
            public static void Prefix(BlEditorManager __instance)
            {
                if (__instance != null && __instance.selectedTransforms != null && __instance.selectedTransforms.Count > 0)
                {
                    List<int> objectIds = new List<int>();

                    foreach (var transform in __instance.selectedTransforms)
                    {
                        NetworkObject networkObject = transform.GetComponent<NetworkObject>();
                        ConsoleBase.WriteLine(transform);
                        ConsoleBase.WriteLine("network object: " + networkObject);
                        if (networkObject != null)
                        {
                            objectIds.Add(networkObject.NetworkId);
                        }
                    }

                    if (objectIds.Count > 0)
                    {
                        ConsoleBase.WriteLine(objectIds.Count);
                        PacketSender.SendRemoveObject(objectIds, PacketDistribution.SendToOthers);
                    }
                }
            }
        }

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

                    // Ne pas appeler SetSelection() ici
                    // MonoBehaviourSingleton<BlEditorManager>.Instance.SetSelection(gameObject.transform);

                    PacketSender.SendPlaceObject(position, Quaternion.identity, Vector3.one, networkObject.NetworkId, fullPath, PacketDistribution.SendToOthers);
                }

                return false;
            }
        }
    }
}
