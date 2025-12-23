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

        public static void updateObject(int objectId, Vector3 position, Quaternion rotation, Vector3 scale, byte[] componentData)
        {
        }

        public static void loadMap(string mapName)
        {
        }

        public static void userSync(int userId, Vector3 position, Quaternion rotation)
        {
            UserAvatar[] userAvatars = UnityEngine.Object.FindObjectsOfType<UserAvatar>();
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
            GameObject userSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            userSphere.name = $"User_{userId}";
            userSphere.transform.position = position;
            userSphere.transform.rotation = rotation;
            userSphere.transform.localScale = Vector3.one * 0.5f;

            Renderer renderer = userSphere.GetComponent<Renderer>();
            Material material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, 0f, 0f, 0.5f);
            renderer.material = material;

            Collider collider = userSphere.GetComponent<Collider>();
            collider.enabled = false;

            UserAvatar avatar = userSphere.AddComponent<UserAvatar>();
            avatar.UserId = userId;

            GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arrow.name = $"User_{userId}_Arrow";
            arrow.transform.parent = userSphere.transform;
            arrow.transform.localPosition = Vector3.forward * 0.5f;
            arrow.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);

            Renderer arrowRenderer = arrow.GetComponent<Renderer>();
            Material arrowMaterial = new Material(Shader.Find("Standard"));
            arrowMaterial.color = Color.white;
            arrowRenderer.material = arrowMaterial;

            Collider arrowCollider = arrow.GetComponent<Collider>();
            arrowCollider.enabled = false;

            GameObject textObj = new GameObject($"User_{userId}_Text");
            textObj.transform.parent = userSphere.transform;
            textObj.transform.localPosition = Vector3.up * 0.6f;

            TextMesh textMesh = textObj.AddComponent<TextMesh>();
            textMesh.text = userId.ToString();
            textMesh.fontSize = 40;
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;

            Renderer textRenderer = textObj.GetComponent<Renderer>();
            textRenderer.material.color = Color.white;
        }

        public static void removeUser(int userId)
        {
            var usersSphere = UnityEngine.Object.FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);
            var userSphere = usersSphere.First(x => x.UserId == userId);
            if (userSphere)
                GameObject.Destroy(userSphere);
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
                    ConsoleBase.WriteError($"Player movement sync error: {ex.Message}");
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

        private void Update()
        {
            currentPosition = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * interpolationSpeed);
            currentRotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * interpolationSpeed);

            transform.position = currentPosition;
            transform.rotation = currentRotation;
        }
    }
}
