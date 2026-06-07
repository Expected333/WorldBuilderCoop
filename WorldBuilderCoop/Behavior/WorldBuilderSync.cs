using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.ResourceDB;
using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    internal class WorldBuilderSync
    {
        public const int TargetFPS = 30;
        public const float SyncInterval = 1f / TargetFPS;
        public const float PositionThreshold = 0.05f;
        public const float RotationThreshold = 1f;
        public static List<long> blacklistSelection = new List<long>();
        public static bool IsRemoteAction = false;

        public static void placeObject(Vector3 position, Quaternion rotation, Vector3 scale, long objectId, string prefabPath, int prefabIndex = -1)
        {
            WbLog.Debug($"[WorldBuilderSync] placeObject called: ID={objectId}, Path={prefabPath}, Index={prefabIndex}");
            try
            {
                var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
                if (sceneManager == null)
                {
                    ConsoleBase.WriteError("SceneManager.Instance is null");
                    return;
                }

                GameObject prefab = null;

                // 1) Résolution par index (= hash du nom de prefab) : MÊME mécanisme fiable que
                //    la synchro de map (ProcessPendingLoadQueue). Indépendant du dossier courant.
                if (sceneManager.TryGetPrefab(prefabIndex, out var byIndex))
                {
                    prefab = byIndex;
                }

                // 2) Fallback chemin ResourceDB (compat anciens paquets / objets sans index).
                if (prefab == null && !string.IsNullOrEmpty(prefabPath))
                {
                    prefab = ResourceDB.Instance.GetFolder(prefabPath)?.LoadRuntime;
                }

                if (prefab == null)
                {
                    ConsoleBase.WriteError($"Failed to load prefab: Path={prefabPath}, Index={prefabIndex}");
                    return;
                }

                if (Core.networkObjectManager.HasNetworkObject(objectId))
                {
                    WbLog.Debug($"[WorldBuilderSync] Object {objectId} already exists. Skipping.");
                    // Object already exists, do not duplicate
                    // Maybe we should update it? But placeObject usually means NEW object.
                    return;
                }

                WbLog.Debug($"[WorldBuilderSync] Instantiating object {objectId}");
                GameObject gameObject = sceneManager.InstantiateEditor(prefab, sceneManager.currentPlace, position, rotation);
                gameObject.transform.localScale = scale;

                var networkObject = gameObject.AddComponent<NetworkObject>();
                networkObject.NetworkId = objectId;
                networkObject.PrefabPath = prefabPath;
                networkObject.PrefabIndex = prefabIndex;

                Core.networkObjectManager.RegisterNetworkObject(objectId, networkObject);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"placeObject error: {ex.Message}");
            }
        }

        public static void destroyObject(List<long> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
                return;

            // Use NetworkObjectManager to find objects efficiently
            foreach (var id in objectIds)
            {
                var networkObject = Core.networkObjectManager.GetNetworkObject(id);
                if (networkObject != null)
                {
                    if (BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
                        BlEditorManager.Instance.selectedTransforms.Remove(networkObject.transform);

                    UnityEngine.Object.Destroy(networkObject.gameObject);
                    Core.networkObjectManager.UnregisterNetworkObject(id);
                }
            }
        }

        public static void updateObject(List<NetTransform> transforms)
        {
            if (transforms == null || transforms.Count == 0) return;

            foreach (var t in transforms)
            {
                NetworkObject networkObject = Core.networkObjectManager.GetNetworkObject(t.objectId);

                if (networkObject == null)
                {
                    var all = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                    networkObject = all.FirstOrDefault(x => x.NetworkId == t.objectId);
                    if (networkObject != null) Core.networkObjectManager.RegisterNetworkObject(t.objectId, networkObject);
                }

                if (networkObject != null && networkObject.GetComponent<UserAvatar>() == null)
                {
                    if (BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
                        BlEditorManager.Instance.RemoveFromSelection(networkObject.transform);

                    var interpolator = networkObject.GetComponent<GameObjectInterpolator>();
                    if (interpolator == null) interpolator = networkObject.gameObject.AddComponent<GameObjectInterpolator>();

                    interpolator.SetTarget(t.position, t.rotation, t.scale);
                }
            }
        }

        public static void DuplicateObject(long sourceId, long newId, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            try
            {
                var sourceObj = Core.networkObjectManager.GetNetworkObject(sourceId);
                if (sourceObj == null)
                {
                    ConsoleBase.WriteError($"[WorldBuilderSync] Source object {sourceId} not found for duplication");
                    return;
                }

                if (Core.networkObjectManager.HasNetworkObject(newId))
                {
                    ConsoleBase.WriteError($"[WorldBuilderSync] New ID {newId} already exists. Skipping duplicate.");
                    return;
                }

                // Conserver le parent (le "place" de l'éditeur) de la source.
                GameObject newGo = UnityEngine.Object.Instantiate(sourceObj.gameObject, position, rotation, sourceObj.transform.parent);
                newGo.transform.localScale = scale;
                // Clean name if needed, but Unity adds (Clone) automatically. 
                // We might want to keep it consistent or remove double (Clone).
                
                var netObj = newGo.GetComponent<NetworkObject>();
                if (netObj == null) netObj = newGo.AddComponent<NetworkObject>();

                netObj.NetworkId = newId;
                netObj.PrefabPath = sourceObj.PrefabPath;
                netObj.PrefabIndex = sourceObj.PrefabIndex;

                Core.networkObjectManager.RegisterNetworkObject(newId, netObj);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[WorldBuilderSync] DuplicateObject error: {ex.Message}");
            }
        }

        public static void AddToSelection(int userId, long objectId)
        {
            NetworkObject networkObject = Core.networkObjectManager.GetNetworkObject(objectId);
            if (networkObject != null && BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
            {
                blacklistSelection.Add(networkObject.NetworkId);
                // Résolution de conflit : un autre joueur a sélectionné cet objet, on le retire
                // de NOTRE sélection. IsRemoteAction empêche le patch RemoveFromSelection de
                // re-broadcaster cette désélection (sinon écho secondaire).
                IsRemoteAction = true;
                BlEditorManager.Instance.RemoveFromSelection(networkObject.transform);
                IsRemoteAction = false;
            }
        }

        public static void RemoveFromSelection(int userId, long objectId)
        {
            NetworkObject networkObject = Core.networkObjectManager.GetNetworkObject(objectId);
            if (networkObject != null)
                blacklistSelection.Remove(networkObject.NetworkId);
        }

        public static IEnumerator listenPlayerMovementLoop()
        {
            Vector3 lastPosition = Vector3.zero;
            Quaternion lastRotation = Quaternion.identity;

            while (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected)
            {
                try
                {
                    var camera = MonoBehaviourSingleton<BlSceneCamera>.Instance;
                    if (camera != null)
                    {
                        Vector3 currentPos = camera.mTransform.position;
                        Quaternion currentRot = camera.mTransform.rotation;

                        if (Vector3.Distance(currentPos, lastPosition) > PositionThreshold ||
                            Quaternion.Angle(currentRot, lastRotation) > RotationThreshold)
                        {
                            int userId = GetCurrentUserId();
                            int placeIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;
                            byte[] data = PlayerSyncHelper.SerializePlayerSync(userId, currentPos, currentRot, placeIndex);
                            SteamNetworkManager.Instance.SendToAll(data);
                            lastPosition = currentPos;
                            lastRotation = currentRot;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ConsoleBase.WriteError("Player movement sync error: " + ex.Message);
                }
                yield return new WaitForSeconds(SyncInterval);
            }
        }

        private static int GetCurrentUserId() => NetworkIdentity.GetUserId();
    }
}
