using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.ResourceDB;
using ModLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilderCoop
{
    internal class WorldBuilderSync
    {
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
            UserAvatar[] userAvatars = UnityEngine.Object.FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);

            foreach (var avatar in userAvatars)
            {
                if (avatar.UserId == userId)
                {
                    avatar.transform.position = position;
                    avatar.transform.rotation = rotation;
                    return;
                }
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

            userSphere.AddComponent<UserAvatar>().UserId = userId;
        }
    }
}
