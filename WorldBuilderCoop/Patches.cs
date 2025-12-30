using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using HarmonyLib;
using ModLoader;
using Steamworks;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using WorldBuilderCoop.Events;
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
                if (GameObject.Find("__WorldBuilderNetwork") != null)
                    return;

                GameObject go = new GameObject("__WorldBuilderNetwork");
                go.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(go);
                go.AddComponent<SteamNetworkManager>();

                if (__instance != null)
                {
                    ConsoleBase.WriteLine(__instance);
                    __instance.StartCoroutine(ApplyBuilderThemeDelayed(__instance));
                }
            }

            private static System.Collections.IEnumerator ApplyBuilderThemeDelayed(BlEditorManager instance)
            {
                yield return new WaitForFixedUpdate();

                try
                {
                    UIHosting.ApplyBlEditorTheme();
                }
                catch (System.Exception ex)
                {
                    ConsoleBase.WriteLine("[WorldBuilder] WB_Patch delayed error: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(BlEditorManager), "AddToSelection")]
        public class BlEditorManagerAddToSelection_Patch
        {
            public static bool Prefix(BlEditorManager __instance, Transform t)
            {
                if (t == null) return false;
                var networkObj = t.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    if (WorldBuilderSync.blacklistSelection.Contains(networkObj.NetworkId))
                        return false;

                    int userId = SteamUser.GetSteamID().m_SteamID.GetHashCode();

                    Core.networkObjectManager.MarkAsSelectedByUser(userId, networkObj.NetworkId);

                    WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, true);
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(BlEditorManager), "RemoveFromSelection")]
        public class BlEditorManagerRemoveFromSelection_Patch
        {
            public static void Postfix(BlEditorManager __instance, Transform t)
            {
                if (t == null) return;
                var networkObj = t.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    int userId = SteamUser.GetSteamID().m_SteamID.GetHashCode();

                    Core.networkObjectManager.UnmarkAsSelectedByUser(userId, networkObj.NetworkId);

                    WorldBuilderEventManager.Instance.RaiseSelectionChanged(userId, networkObj.NetworkId, false);
                }
            }
        }

        [HarmonyPatch(typeof(BlEditorManager), "UpdateHandleMove")]
        public class BlEditorManagerUpdateHandleMove_Patch2
        {
            private static float _lastSendTime;
            private const int TARGET_FPS = 20;
            private static float sendInterval => 1.0f / TARGET_FPS;
            private static Vector3 _lastPosition = Vector3.zero;
            private static Quaternion _lastRotation = Quaternion.identity;
            private static Vector3 _lastScale = Vector3.one;
            private const float POSITION_THRESHOLD = 0.01f;
            private const float ROTATION_THRESHOLD = 0.5f;
            private const float SCALE_THRESHOLD = 0.001f;

            public static void Postfix(BlEditorManager __instance)
            {
                if (__instance == null || __instance.selectedTransforms == null || __instance.selectedTransforms.Count == 0)
                    return;

                if (Time.time - _lastSendTime < sendInterval) return;

                List<int> objectIds = new List<int>();
                Vector3 position = Vector3.zero;
                Quaternion rotation = Quaternion.identity;
                Vector3 scale = Vector3.one;
                bool hasValidObject = false;

                for (int i = 0; i < __instance.selectedTransforms.Count; i++)
                {
                    Transform transform = __instance.selectedTransforms[i];
                    NetworkObject networkObject = transform.GetComponent<NetworkObject>();

                    if (networkObject != null)
                    {
                        objectIds.Add(networkObject.NetworkId);
                        hasValidObject = true;

                        if (i == 0)
                        {
                            position = transform.position;
                            rotation = transform.rotation;
                            scale = transform.localScale;
                        }
                    }
                }

                if (hasValidObject && objectIds.Count > 0)
                {
                    bool positionChanged = Vector3.Distance(position, _lastPosition) > POSITION_THRESHOLD;
                    bool rotationChanged = Quaternion.Angle(rotation, _lastRotation) > ROTATION_THRESHOLD;
                    bool scaleChanged = Vector3.Distance(scale, _lastScale) > SCALE_THRESHOLD;

                    if (positionChanged || rotationChanged || scaleChanged)
                    {
                        _lastSendTime = Time.time;
                        _lastPosition = position;
                        _lastRotation = rotation;
                        _lastScale = scale;

                        WorldBuilderEventManager.Instance.RaiseObjectMoved(objectIds, position, rotation, scale);
                    }
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
                        if (networkObject != null)
                        {
                            objectIds.Add(networkObject.NetworkId);
                        }
                    }

                    if (objectIds.Count > 0)
                        WorldBuilderEventManager.Instance.RaiseObjectRemoved(objectIds);
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

                    MonoBehaviourSingleton<BlEditorManager>.Instance.SetSelection(gameObject.transform);

                    WorldBuilderEventManager.Instance.RaiseObjectPlaced(networkObject.NetworkId, fullPath, position, Quaternion.identity, Vector3.one);
                }

                return false;
            }
        }

        [HarmonyPatch(typeof(BrokeProtocol.Client.Builder.BlEditorManager), "LoadMap")]
        public class BlEditorManagerLoadMap_Patch
        {
            public static void Postfix(BrokeProtocol.Client.UI.BlPrefabItemButton __instance)
            {
                if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected)
                {
                    if (SteamNetworkManager.Instance.IsHost)
                    {
                        byte[] data = SerializeLoadMap(WorldBuilderSync.GetMapsObjects());
                        SteamNetworkManager.Instance.SendToAll(data);
                    }
                }
            }

            private static byte[] SerializeLoadMap(List<ObjectInfo> objects)
            {
                int chunkSize = 30;
                int totalObjects = objects.Count;
                byte[] lastChunk = new byte[0];

                for (int i = 0; i < totalObjects; i += chunkSize)
                {
                    List<ObjectInfo> currentChunk = objects.GetRange(i, System.Math.Min(chunkSize, totalObjects - i));
                    bool isFirstChunk = (i == 0);
                    bool isLastChunk = (i + chunkSize >= totalObjects);

                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((byte)Packets.LoadMap);
                            writer.Write(currentChunk.Count);
                            writer.Write(isFirstChunk);
                            writer.Write(totalObjects);
                            writer.Write(isLastChunk);

                            foreach (var obj in currentChunk)
                            {
                                writer.Write(obj.objectId);
                                writer.Write(obj.position.x); writer.Write(obj.position.y); writer.Write(obj.position.z);
                                writer.Write(obj.rotation.x); writer.Write(obj.rotation.y); writer.Write(obj.rotation.z); writer.Write(obj.rotation.w);
                                writer.Write(obj.scale.x); writer.Write(obj.scale.y); writer.Write(obj.scale.z);
                                writer.Write(obj.prefabIndex);
                                writer.Write(obj.placeIndex);
                            }

                            lastChunk = ms.ToArray();
                        }
                    }

                    // Send each chunk
                    SteamNetworkManager.Instance.SendToAll(lastChunk);

                    if (i + chunkSize < totalObjects)
                    {
                        // Small delay between chunks
                        System.Threading.Thread.Sleep(200);
                    }
                }

                return lastChunk;
            }
        }
    }
}