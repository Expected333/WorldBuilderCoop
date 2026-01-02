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
        public static List<int> blacklistSelection = new List<int>();
        public static bool IsRemoteAction = false;

        public static void placeObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabPath, int prefabIndex = -1)
        {
            ConsoleBase.WriteLine($"[WorldBuilderSync] placeObject called: ID={objectId}, Path={prefabPath}, Index={prefabIndex}");
            try
            {
                var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
                if (sceneManager == null)
                {
                    ConsoleBase.WriteError("SceneManager.Instance is null");
                    return;
                }

                ResourceItem folderItem = null;

                if (!string.IsNullOrEmpty(prefabPath))
                {
                    folderItem = ResourceDB.Instance.GetFolder(prefabPath);
                }
                else if (prefabIndex >= 0)
                {
                    // Attempt to get by index if path is missing
                    if (ResourceDB.Instance.resources != null && prefabIndex < ResourceDB.Instance.resources.Count)
                    {
                        folderItem = ResourceDB.Instance.resources[prefabIndex];
                    }
                }

                var prefab = folderItem?.LoadRuntime;

                if (prefab == null)
                {
                    ConsoleBase.WriteError($"Failed to load prefab: Path={prefabPath}, Index={prefabIndex}");
                    return;
                }

                if (Core.networkObjectManager.HasNetworkObject(objectId))
                {
                    ConsoleBase.WriteLine($"[WorldBuilderSync] Object {objectId} already exists. Skipping.");
                    // Object already exists, do not duplicate
                    // Maybe we should update it? But placeObject usually means NEW object.
                    return;
                }

                ConsoleBase.WriteLine($"[WorldBuilderSync] Instantiating object {objectId}");
                GameObject gameObject = sceneManager.InstantiateEditor(prefab, sceneManager.currentPlace, position, rotation);
                gameObject.transform.localScale = scale;

                var networkObject = gameObject.AddComponent<NetworkObject>();
                networkObject.NetworkId = objectId;
                networkObject.PrefabPath = !string.IsNullOrEmpty(prefabPath) ? prefabPath : folderItem.path;
                networkObject.PrefabIndex = prefabIndex;

                Core.networkObjectManager.RegisterNetworkObject(objectId, networkObject);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"placeObject error: {ex.Message}");
            }
        }

        public static void destroyObject(List<int> objectIds)
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

        public static void updateObject(List<int> objectIds, Vector3 position, Quaternion rotation, Vector3 scale, byte[] componentData)
        {
            if (objectIds == null || objectIds.Count == 0) return;

            foreach (int id in objectIds)
            {
                NetworkObject networkObject = Core.networkObjectManager.GetNetworkObject(id);

                if (networkObject == null)
                {
                    var all = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                    networkObject = all.FirstOrDefault(x => x.NetworkId == id);
                    if (networkObject != null) Core.networkObjectManager.RegisterNetworkObject(id, networkObject);
                }

                if (networkObject != null && networkObject.GetComponent<UserAvatar>() == null)
                {
                    if (BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
                        BlEditorManager.Instance.RemoveFromSelection(networkObject.transform);

                    var interpolator = networkObject.GetComponent<GameObjectInterpolator>();
                    if (interpolator == null) interpolator = networkObject.gameObject.AddComponent<GameObjectInterpolator>();

                    interpolator.SetTarget(position, rotation, scale);
                }
            }
        }

        public static void AddToSelection(int userId, int objectId)
        {
            NetworkObject networkObject = Core.networkObjectManager.GetNetworkObject(objectId);
            if (networkObject != null && BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
            {
                blacklistSelection.Add(networkObject.NetworkId);
                BlEditorManager.Instance.RemoveFromSelection(networkObject.transform);
            }
        }

        public static void RemoveFromSelection(int userId, int objectId)
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

        private static int GetCurrentUserId()
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsLocalMode())
            {
                return LocalUserManager.GetLocalUserId();
            }
            return Steamworks.SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }
    }
}
