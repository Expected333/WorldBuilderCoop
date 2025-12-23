using BrokeProtocol.Client.Builder;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using BrokeProtocol.Utility.ResourceDB;
using ModLoader;
using System;
using UnityEngine;

namespace WorldBuilderCoop
{
    internal class WorldBuilderSync
    {
        public static void placeObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabPath)
        {
            try
            {
                ResourceItem folderItem = ResourceDB.Instance.GetFolder(prefabPath);

                if (folderItem == null)
                {
                    ConsoleBase.WriteError($"ResourceItem not found at: {prefabPath}");
                    return;
                }

                GameObject prefab = folderItem.LoadRuntime;

                if (prefab == null)
                {
                    ConsoleBase.WriteError($"Prefab is null for: {prefabPath}");
                    return;
                }

                GameObject gameObject = MonoBehaviourSingleton<SceneManager>.Instance.InstantiateEditor(
                    prefab,
                    MonoBehaviourSingleton<SceneManager>.Instance.currentPlace,
                    position,
                    rotation
                );

                gameObject.transform.localScale = scale;
                MonoBehaviourSingleton<BlEditorManager>.Instance.SetSelection(gameObject.transform);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"placeObject error: {ex.Message}");
            }
        }

        public static void destroyObject(int objectId)
        {
        }

        public static void updateObject(int objectId, Vector3 position, Quaternion rotation, Vector3 scale, byte[] componentData)
        {
        }

        public static void loadMap(string mapName)
        {
        }

        public static void userSync(int userId, Vector3 position, Quaternion rotation)
        {
        }
    }
}
