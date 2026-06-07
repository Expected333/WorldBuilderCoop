using BrokeProtocol.Client.Builder;
using BrokeProtocol.Parameters;
using HarmonyLib;
using ModLoader;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Events;
using WorldBuilderCoop.Network;
using WorldBuilderCoop.Managers;

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
                WbLog.Debug(__instance);
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
                WbLog.Debug("[WorldBuilder] WB_Patch delayed error: " + ex.Message);
            }
        }
    }

    // Associe chaque snapshot d'historique du jeu à la liste ordonnée des NetworkId.
    // SerializeLevel(checkSave:false) est l'appel utilisé par AppendHistory pour produire un snapshot.
    [HarmonyPatch(typeof(BrokeProtocol.Managers.SceneManager), "SerializeLevel")]
    public class SceneManagerSerializeLevel_Patch
    {
        public static void Postfix(bool checkSave, List<BaseParameters> __result)
        {
            if (checkSave) return; // snapshots d'historique = checkSave:false
            if (WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance != null)
            {
                WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance.AssociateSnapshot(__result);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "ReadLevel", new System.Type[] { typeof(List<BaseParameters>) })]
    public class BlEditorManagerReadLevel_Patch
    {
        public static void Prefix()
        {
            // Vide les objets (garde les avatars) : ils seront réenregistrés par index.
            Core.networkObjectManager?.ClearObjects();
        }

        public static void Postfix(BlEditorManager __instance, List<BaseParameters> parameters)
        {
            if (WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance != null)
            {
                WorldBuilderCoop.Behavior.WorldBuilderHistoryManager.Instance.RestoreSnapshot(parameters);
            }
        }
    }

    [HarmonyPatch(typeof(BlEditorManager), "StartHandleMove")]
    public class BlEditorManagerStartHandleMove_Patch
    {
        public static void Prefix(BlEditorManager __instance)
        {
            // if (MapManager.IsSyncing) return;
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
        private static Vector3 _lastPosition;
        private static Quaternion _lastRotation;
        private static Vector3 _lastScale;
        private const float MOVE_THRESHOLD = 0.01f;
        private const float ROTATION_THRESHOLD = 0.1f;
        private const float SCALE_THRESHOLD = 0.01f;

        public static void Postfix(BlEditorManager __instance)
        {
            if (WorldBuilderSync.IsRemoteAction || SteamNetworkManager.Instance == null) return;
            // if (MapManager.IsSyncing) return;

            if (Time.time - _lastSendTime < sendInterval) return;

            var transforms = new List<NetTransform>();
            // Transform du pivot (1er objet sélectionné) pour le seuil de changement.
            Vector3 pivotPos = Vector3.zero;
            Quaternion pivotRot = Quaternion.identity;
            Vector3 pivotScale = Vector3.one;
            bool hasValidObject = false;

            for (int i = 0; i < __instance.selectedTransforms.Count; i++)
            {
                Transform transform = __instance.selectedTransforms[i];
                NetworkObject networkObject = transform.GetComponent<NetworkObject>();

                if (networkObject != null)
                {
                    transforms.Add(new NetTransform
                    {
                        objectId = networkObject.NetworkId,
                        position = transform.position,
                        rotation = transform.rotation,
                        scale = transform.localScale
                    });

                    if (!hasValidObject)
                    {
                        pivotPos = transform.position;
                        pivotRot = transform.rotation;
                        pivotScale = transform.localScale;
                        hasValidObject = true;
                    }
                }
            }

            if (hasValidObject)
            {
                bool positionChanged = Vector3.Distance(pivotPos, _lastPosition) > MOVE_THRESHOLD;
                bool rotationChanged = Quaternion.Angle(pivotRot, _lastRotation) > ROTATION_THRESHOLD;
                bool scaleChanged = Vector3.Distance(pivotScale, _lastScale) > SCALE_THRESHOLD;

                if (positionChanged || rotationChanged || scaleChanged)
                {
                    _lastSendTime = Time.time;
                    _lastPosition = pivotPos;
                    _lastRotation = pivotRot;
                    _lastScale = pivotScale;

                    WorldBuilderEventManager.Instance.RaiseObjectMoved(transforms);
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
                List<long> objectIds = new List<long>();

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
            if (WorldBuilderSync.IsRemoteAction || SteamNetworkManager.Instance == null || __instance.selectedTransforms.Count == 0)
                return;

            // Après DuplicateSelection, selectedTransforms = les nouvelles copies.
            // Chaque copie hérite du NetworkId de sa source (composant copié) :
            // on l'utilise comme sourceId, puis on alloue un ID neuf sans collision.
            var entries = new List<DuplicateEntry>();

            foreach (var t in __instance.selectedTransforms)
            {
                var netObj = t.GetComponent<NetworkObject>();
                if (netObj == null) continue;

                long sourceId = netObj.NetworkId;
                long newId = NetworkIdAllocator.Allocate();
                netObj.NetworkId = newId;

                // Enregistrer la copie locale pour qu'elle soit adressable immédiatement.
                Core.networkObjectManager.RegisterNetworkObject(newId, netObj);

                entries.Add(new DuplicateEntry
                {
                    sourceId = sourceId,
                    newId = newId,
                    position = t.position,
                    rotation = t.rotation,
                    scale = t.localScale
                });
            }

            if (entries.Count > 0 && SelectionBatcher.Instance != null)
            {
                SelectionBatcher.Instance.RequestDuplicate(entries);
            }
        }
    }
}
