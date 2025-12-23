using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using HarmonyLib;
using ModLoader;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

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
            public static void Postfix(BlEditorManager __instance)
            {
                ConsoleBase.WriteLine("pmlsdjklkqedjq");
                if (__instance != null && __instance.selectedTransforms != null && __instance.selectedTransforms.Count > 0)
                {
                    List<int> objectIds = new List<int>();

                    foreach (var transform in __instance.selectedTransforms)
                    {
                        NetworkObject networkObject = transform.GetComponent<NetworkObject>();
                        if (networkObject != null)
                        {
                            objectIds.Add(networkObject.NetworkId);
                        }
                    }

                    if (objectIds.Count > 0)
                    {
                        ConsoleBase.WriteLine(objectIds.ToArray());
                        Core.Network.SendRemoveObject(objectIds, PacketDistribution.SendToOthers);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BrokeProtocol.Client.UI.BlPrefabItemButton), "Clicked")]
        public class BlPrefabItemButton_Patch
        {
            public static void Postfix(BrokeProtocol.Client.UI.BlPrefabItemButton __instance)
            {
                GameObject spawnedObject = null;
                var originalMethod = typeof(BrokeProtocol.Client.UI.BlPrefabItemButton).GetMethod("Clicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
                var blEditorManager = MonoBehaviourSingleton<BlEditorManager>.Instance;
                if (blEditorManager.objectInspector is BlPrefabObjectInspector || (bool)blEditorManager.itemOptionInspector || __instance.CompareTag("Folder"))
                {
                    return;
                }
                Transform mTransform = MonoBehaviourSingleton<BlSceneCamera>.Instance.mTransform;
                Ray ray = new Ray(mTransform.position, mTransform.forward);
                Vector3 hitPoint;
                Transform obj;
                Vector3 position = ((!blEditorManager.ObjectRaycast(ray, out obj, out hitPoint)) ? MonoBehaviourSingleton<BlSceneCamera>.Instance.RoundedPivot : hitPoint.Snap(0.01f));
                spawnedObject = sceneManager.InstantiateEditor(Traverse.Create(__instance).Field("asset").GetValue() as GameObject, sceneManager.currentPlace, position, Quaternion.identity);
                if (spawnedObject != null)
                {
                    Vector3 scale = spawnedObject.transform.localScale;
                    Quaternion rotation = spawnedObject.transform.rotation;
                    int objectId = spawnedObject.GetInstanceID();
                    string prefabName = spawnedObject.name;

                    string currentPath = blEditorManager.currentPrefabPath;
                    string fullPath = Path.Combine(currentPath, prefabName);

                    Core.Network.SendPlaceObject(position, rotation, scale, objectId, fullPath, PacketDistribution.SendToOthers);
                }
            }
        }
    }
}
