using BrokeProtocol.Client.Builder;
using BrokeProtocol.Entities;
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

        public static void placeObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabPath)
        {
            try
            {
                var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
                if (sceneManager == null)
                {
                    ConsoleBase.WriteError("SceneManager.Instance is null");
                    return;
                }

                var folderItem = ResourceDB.Instance.GetFolder(prefabPath);
                var prefab = folderItem?.LoadRuntime;

                if (prefab == null)
                {
                    ConsoleBase.WriteError($"Failed to load prefab: {prefabPath}");
                    return;
                }

                GameObject gameObject = sceneManager.InstantiateEditor(prefab, sceneManager.currentPlace, position, rotation);
                gameObject.transform.localScale = scale;

                var networkObject = gameObject.AddComponent<NetworkObject>();
                networkObject.NetworkId = objectId;
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"placeObject error: {ex.Message}");
            }
        }

        public static void destroyObject(List<int> objectIds)
        {
            if (objectIds == null || objectIds.Count == 0)
            {
                return;
            }

            NetworkObject[] networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);

            foreach (var networkObject in networkObjects)
            {
                ConsoleBase.WriteLine("network object found " + networkObject);
                if (objectIds.Contains(networkObject.NetworkId))
                {
                    if (BlEditorManager.Instance.selectedTransforms.Contains(networkObject.transform))
                        BlEditorManager.Instance.selectedTransforms.Remove(networkObject.transform);
                    UnityEngine.Object.Destroy(networkObject.gameObject);
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

                    if (componentData != null && componentData.Length > 0)
                    {
                    }
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

        private static int tempCount = 0;

        public static void loadMap(List<ObjectInfo> objects, bool clear, int totalObjectsCount, bool isLastChunk)
        {
            ConsoleBase.WriteLine("Loading map chunk with " + objects.Count + " objects. Clear: " + clear);
            //preprocess
            BlEditorManager.Instance.ClearSelection();
            if (clear)
            {
                MonoBehaviourSingleton<BrokeProtocol.Managers.SceneManager>.Instance.Clear();
                SceneManager.Instance.ResetLoadingWindow(totalObjectsCount);
            }

            foreach (ObjectInfo obj in objects)
            {
                if (!SceneManager.Instance.TryGetPrefab(obj.prefabIndex, out var prefab))
                {
                    prefab = SceneManager.Instance.GetPrefab("NullObject");
                    ConsoleBase.WriteError("Cannot find prefab with index: " + obj.prefabIndex);
                }

                SceneManager.Instance.SetMinPlaces(obj.placeIndex);
                GameObject gameObject = SceneManager.Instance.InstantiatePrefab(prefab, obj.placeIndex, obj.position, obj.rotation);
                UpdateObject(gameObject.transform, obj);
                SceneManager.Instance.IncrementTransferProgress(1);
                if (Core.Network.IsConnected)
                {
                    InitializeEditor(gameObject);
                }
                tempCount++;
            }

            if (isLastChunk)
            {
                ConsoleBase.WriteLine("loaded prefabs : " + tempCount);
                tempCount = 0;
            }
        }

        public static void UpdateObject(Transform t, ObjectInfo obj)
        {
            if (obj.placeIndex >= 0)
            {
                t.SetParent(MonoBehaviourSingleton<SceneManager>.Instance.mTransform.GetChild(obj.placeIndex), worldPositionStays: true);
            }
            else
            {
                t.SetParent(null, worldPositionStays: true);
            }
            t.SetPositionAndRotation(obj.position, obj.rotation);
            t.localScale = obj.scale;
        }

        private static void InitializeEditor(GameObject go)
        {
            MonoBehaviour[] componentsInChildren = go.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour monoBehaviour in componentsInChildren)
            {
                if (Attribute.GetCustomAttribute(monoBehaviour.GetType(), typeof(GizmoComponentAttribute)) != null)
                {
                    monoBehaviour.enabled = false;
                    UnityEngine.Object.Destroy(monoBehaviour);
                }
            }
            if (go.TryGetComponent<Serialized>(out var component))
            {
                component.InitializeEditor();
            }
            else
            {
                go.InitializeEditor();
            }
            go.GetMesh(out var _);
            UpdateRendering(go);
        }

        private static void UpdateRendering(GameObject g)
        {
            LayerType[] array = SceneManager.Instance.layerTypes;
            foreach (LayerType layerType in array)
            {
                if (layerType.type != null)
                {
                    if (!g.GetComponent(layerType.type))
                    {
                        continue;
                    }
                }
                else if ((bool)g.GetComponent<Serialized>())
                {
                    continue;
                }
                if (layerType.visible)
                {
                    if (!g.activeSelf)
                    {
                        g.SetActive(value: true);
                    }
                    continue;
                }
                if (g.activeSelf)
                {
                    g.SetActive(value: false);
                }
                break;
            }
            BlGizmoForceIcon component2;
            if (SceneManager.Instance.forceGizmos)
            {
                if (!g.TryGetComponent<BlGizmoForceIcon>(out var component) || !component.enabled)
                {
                    g.AddComponent<BlGizmoForceIcon>();
                }
            }
            else if (g.TryGetComponent<BlGizmoForceIcon>(out component2))
            {
                component2.enabled = false;
                UnityEngine.Object.Destroy(component2);
            }
        }

        public static List<ObjectInfo> GetMapsObjects()
        {
            var list = new List<ObjectInfo>();
            var list2 = SceneManager.Instance.AllTransforms();
            foreach (Transform item2 in list2)
            {
                var networkObj = item2.GetComponent<NetworkObject>();
                if (networkObj == null)
                    item2.gameObject.AddComponent<NetworkObject>();
                else
                {
                    list.Add(new ObjectInfo()
                    {
                        objectId = networkObj.NetworkId,
                        position = item2.transform.position,
                        rotation = item2.transform.rotation,
                        scale = item2.transform.localScale,
                        prefabIndex = item2.GetPrefabIndex(),
                        placeIndex = item2.parent.GetSiblingIndex(),
                    });
                }
            }
            return list;
        }

        public static void userSync(int userId, Vector3 position, Quaternion rotation)
        {
            UserAvatar[] userAvatars = UnityEngine.Object.FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);
            UserAvatar foundAvatar = null;
            foreach (var avatar in userAvatars)
            {
                if (avatar.UserId == userId)
                {
                    foundAvatar = avatar;
                    break;
                }
            }
            if (foundAvatar == null)
            {
                addUser(userId, position, rotation);
            }
            else
            {
                var interpolator = foundAvatar.GetComponent<UserInterpolator>();
                if (interpolator == null)
                {
                    interpolator = foundAvatar.gameObject.AddComponent<UserInterpolator>();
                }
                interpolator.SetTarget(position, rotation);
            }
        }

        public static void addUser(int userId, Vector3 position, Quaternion rotation)
        {
            if (userId == Core.Network.MyUserId) return;

            UserAvatar[] currentAvatars = UnityEngine.Object.FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);
            foreach (var existing in currentAvatars)
            {
                if (existing.UserId == userId) return;
            }

            GameObject userSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            userSphere.name = "User_" + userId;
            userSphere.transform.position = position;
            userSphere.transform.rotation = rotation;
            userSphere.transform.localScale = Vector3.one * 0.5f;

            Renderer renderer = userSphere.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 0f, 0f, 0.5f);
            renderer.material = material;

            userSphere.GetComponent<Collider>().enabled = false;

            UserAvatar avatar = userSphere.AddComponent<UserAvatar>();
            avatar.UserId = userId;
            avatar.position = position;
            avatar.rotation = rotation;
            avatar.placeIndex = 0;

            List<UserAvatar> avatarList = Core.Network.connectedClientAvatar;
            if (avatarList != null)
            {
                avatarList.Add(avatar);
            }

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = "User_" + userId + "_Arrow";
            arrow.transform.parent = userSphere.transform;
            arrow.transform.localPosition = Vector3.forward * 0.5f;
            arrow.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);

            Renderer arrowRenderer = arrow.GetComponent<Renderer>();
            Material arrowMaterial = new Material(Shader.Find("Standard"));
            arrowMaterial.color = Color.white;
            arrowRenderer.material = arrowMaterial;
            arrow.GetComponent<Collider>().enabled = false;

            AvatarTextDisplay textDisplay = userSphere.AddComponent<AvatarTextDisplay>();
            textDisplay.Initialize(userId.ToString());
        }

        public static void removeUser(int userId)
        {
            UserAvatar[] userAvatars = UnityEngine.Object.FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);
            UserAvatar toRemove = null;

            foreach (var avatar in userAvatars)
            {
                if (avatar.UserId == userId)
                {
                    toRemove = avatar;
                    break;
                }
            }

            if (toRemove != null)
            {
                List<UserAvatar> avatarList = Core.Network.connectedClientAvatar;
                if (avatarList != null)
                {
                    avatarList.Remove(toRemove);
                }
                UnityEngine.Object.Destroy(toRemove.gameObject);
            }
        }

        public static IEnumerator listenPlayerMovementLoop()
        {
            Vector3 lastPosition = Vector3.zero;
            Quaternion lastRotation = Quaternion.identity;

            while (Core.Network.IsConnected)
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
                            PacketSender.SendPlayerSync(Core.Network.MyUserId, currentPos, currentRot, PacketDistribution.SendToOthers);
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
            yield break;
        }
    }

    public class UserInterpolator : MonoBehaviour
    {
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 currentPosition;
        private Quaternion currentRotation;
        private float interpolationSpeed = 10f;

        public void SetTarget(Vector3 newPosition, Quaternion newRotation)
        {
            targetPosition = newPosition;
            targetRotation = newRotation;
        }

        public void Update()
        {
            currentPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            currentRotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);

            transform.position = currentPosition;
            transform.rotation = currentRotation;
        }
    }

    public class GameObjectInterpolator : MonoBehaviour
    {
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 targetScale;
        private float interpolationSpeed = 15f;
        private float snapDistance = 10f;

        public void SetTarget(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            targetPosition = newPosition;
            targetRotation = newRotation;
            targetScale = newScale;

            if (Vector3.Distance(transform.position, targetPosition) > snapDistance)
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
                transform.localScale = targetScale;
            }
        }

        public void Update()
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * interpolationSpeed);
        }
    }
}