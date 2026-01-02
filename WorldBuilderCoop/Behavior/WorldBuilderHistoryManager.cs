using BrokeProtocol.Client.Builder;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Behavior
{
    public class WorldBuilderHistoryManager : MonoBehaviour
    {
        public static WorldBuilderHistoryManager Instance;

        private void Awake()
        {
            Instance = this;
        }

        [System.Serializable]
        public struct NetworkSnapshotItem
        {
            public int NetworkId;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public string PrefabName;
            public string PrefabPath;
            public int PrefabIndex;
        }

        private List<List<NetworkSnapshotItem>> history = new List<List<NetworkSnapshotItem>>();
        private FieldInfo historyIndexField;
        private FieldInfo historyField;
        private int lastEditorHistoryCount = 0;

        private void InitializeReflection()
        {
            historyIndexField = typeof(BlEditorManager).GetField("historyIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            historyField = typeof(BlEditorManager).GetField("history", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void RecordState(BlEditorManager editor, bool undone)
        {
            if (historyIndexField == null) InitializeReflection();

            IList editorHistory = (IList)historyField.GetValue(editor);
            int currentCount = editorHistory.Count;

            // Sync list length (trim forward history)
            if (currentCount < history.Count)
            {
                history.RemoveRange(currentCount, history.Count - currentCount);
            }

            if (!undone)
            {
                bool shouldAdd = false;
                bool shouldShift = false;

                if (currentCount > history.Count)
                {
                    shouldAdd = true;
                }
                else if (currentCount == history.Count && currentCount == 10) // Max history size
                {
                    shouldShift = true;
                }

                if (shouldShift)
                {
                    history.RemoveAt(0);
                    shouldAdd = true;
                }

                if (shouldAdd)
                {
                    List<NetworkSnapshotItem> snapshot = new List<NetworkSnapshotItem>();
                    var objects = FindObjectsOfType<NetworkObject>();

                    foreach (var obj in objects)
                    {
                        snapshot.Add(new NetworkSnapshotItem
                        {
                            NetworkId = obj.NetworkId,
                            Position = obj.transform.position,
                            Rotation = obj.transform.rotation,
                            Scale = obj.transform.localScale,
                            PrefabName = obj.name.Replace("(Clone)", "").Trim(),
                            PrefabPath = obj.PrefabPath,
                            PrefabIndex = obj.PrefabIndex
                        });
                    }
                    history.Add(snapshot);
                }
            }

            lastEditorHistoryCount = currentCount;
        }

        public void RestoreState(BlEditorManager editor)
        {
            if (historyIndexField == null) InitializeReflection();
            int index = (int)historyIndexField.GetValue(editor);

            if (index >= 0 && index < history.Count)
            {
                var snapshot = history[index];

                var allTransforms = BrokeProtocol.Managers.SceneManager.Instance.ActiveGameObjects();
                List<Transform> candidates = new List<Transform>();
                foreach (var t in allTransforms)
                {
                    candidates.Add(t);
                }

                foreach (var snap in snapshot)
                {
                    Transform bestMatch = null;
                    float bestDist = 0.5f;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        string tName = t.name.Replace("(Clone)", "").Trim();
                        if (tName != snap.PrefabName) continue;

                        float dist = Vector3.Distance(t.position, snap.Position);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestMatch = t;
                        }
                    }

                    if (bestMatch != null)
                    {
                        var netObj = bestMatch.GetComponent<NetworkObject>();
                        if (netObj == null) netObj = bestMatch.gameObject.AddComponent<NetworkObject>();
                        netObj.NetworkId = snap.NetworkId;
                        netObj.PrefabIndex = snap.PrefabIndex;
                        netObj.PrefabPath = snap.PrefabPath;

                        // Register with manager
                        if (Core.networkObjectManager != null)
                        {
                            Core.networkObjectManager.RegisterNetworkObject(netObj.NetworkId, netObj);
                        }

                        candidates.Remove(bestMatch);
                    }
                }
            }
        }
    }
}
