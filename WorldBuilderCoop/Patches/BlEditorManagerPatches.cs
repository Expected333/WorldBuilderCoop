using BrokeProtocol.Client.Builder;
using BrokeProtocol.Parameters;
using HarmonyLib;
using ModLoader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Events;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Patches
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
            go.AddComponent<WorldBuilderCoop.Behavior.PlayerSyncTracker>();
            go.AddComponent<WorldBuilderCoop.Behavior.WorldBuilderHistoryManager>();

            if (__instance != null)
            {
                ConsoleBase.WriteLine(__instance);
                __instance.StartCoroutine(ApplyBuilderThemeDelayed(__instance));
            }
        }

        private static IEnumerator ApplyBuilderThemeDelayed(BlEditorManager instance)
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

    [HarmonyPatch(typeof(BlEditorManager), "AppendHistory")]
    public class BlEditorManagerAppendHistory_Patch
    {
        public static void Postfix(BlEditorManager __instance, bool undone)
        {
            if (WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance != null)
            {
                WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance.RecordState(__instance, undone);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "ReadLevel", new System.Type[] { typeof(List<BaseParameters>) })]
    public class BlEditorManagerReadLevel_Patch
    {
        public static void Prefix()
        {
            if (Core.networkObjectManager != null)
            {
                Core.networkObjectManager.ClearAll();
            }
        }

        public static void Postfix(BlEditorManager __instance)
        {
            if (WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance != null)
            {
                WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance.RestoreState(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "StartHandleMove")]
    public class BlEditorManagerStartHandleMove_Patch
    {
        public static void Prefix(BlEditorManager __instance)
        {
            if (!WorldBuilderSync.IsRemoteAction && SteamNetworkManager.Instance != null)
            {
                byte[] data = new byte[] { (byte)Packets.SaveHistory };
                SteamNetworkManager.Instance.SendToAll(data);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "UpdateHandleMove")]
    public class BlEditorManagerUpdateHandleMove_Patch
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

            if (Time.time - _lastSendTime < sendInterval)
                return;

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

    [HarmonyPatch(typeof(BlEditorManager), "Undo")]
    public class BlEditorManagerUndo_Patch
    {
        public static void Prefix(BlEditorManager __instance)
        {
            if (!WorldBuilderSync.IsRemoteAction && SteamNetworkManager.Instance != null)
            {
                byte[] data = new byte[] { (byte)Packets.Undo };
                SteamNetworkManager.Instance.SendToAll(data);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "Redo")]
    public class BlEditorManagerRedo_Patch
    {
        public static void Prefix(BlEditorManager __instance)
        {
            if (!WorldBuilderSync.IsRemoteAction && SteamNetworkManager.Instance != null)
            {
                byte[] data = new byte[] { (byte)Packets.Redo };
                SteamNetworkManager.Instance.SendToAll(data);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "DuplicateSelection")]
    public class BlEditorManagerDuplicateSelection_Patch
    {
        public static void Postfix(BlEditorManager __instance)
        {
            // Only handle if we are connected and it's a local action (not triggered by remote packet)
            if (!WorldBuilderSync.IsRemoteAction && SteamNetworkManager.Instance != null && __instance.selectedTransforms.Count > 0)
            {
                List<int> objectIds = new List<int>();

                // The objects in selectedTransforms are the NEW copies (DuplicateSelection selects them)
                foreach (var t in __instance.selectedTransforms)
                {
                    var netObj = t.GetComponent<NetworkObject>();

                    // If the object was copied from a NetworkObject, it will have the component but with the OLD NetworkId
                    // We need to assign a NEW NetworkId
                    if (netObj != null)
                    {
                        // Generate a new unique ID
                        int newId = System.Guid.NewGuid().GetHashCode();
                        netObj.NetworkId = newId;

                        // Now we need to broadcast this new object creation to others
                        // We use PlaceObject packet because for others, it's effectively a new object

                        // We need to gather prefab info
                        string prefabPath = netObj.PrefabPath;
                        int prefabIndex = netObj.PrefabIndex;

                        // Serialize PlaceObject
                        // Note: We need to access WorldBuilderEventManager to serialize, or call WorldBuilderSync logic?
                        // Actually, we should use WorldBuilderEventManager to raise the event or send packet directly.
                        // WorldBuilderEventManager has SerializePlaceObject but it's private.
                        // We can expose a method in WorldBuilderEventManager or replicate serialization here.
                        // Better: Use WorldBuilderEventManager.RaiseObjectPlaced if available or similar.

                        // Check WorldBuilderEventManager
                        WorldBuilderEventManager.Instance.RaiseObjectPlaced(newId, prefabPath, t.position, t.rotation, t.localScale, prefabIndex);
                    }
                }
            }
        }
    }
}
