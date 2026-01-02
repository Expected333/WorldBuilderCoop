using BrokeProtocol.Client.Builder;
using BrokeProtocol.Entities;
using BrokeProtocol.Managers;
using BrokeProtocol.Parameters;
using BrokeProtocol.Utility;
using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Managers
{
    public static class MapManager
    {
        private static byte[] _incomingBuffer;
        private static int _receivedBytesCount;
        private static readonly List<ObjectInfo> _pendingLoadObjects = new List<ObjectInfo>();
        private static readonly bool _isProcessingLoad = false;
        public static bool IsLoading { get; private set; } = false;

        public static void HandleIncomingChunk(int totalSize, int offset, byte[] data)
        {
            if (_incomingBuffer == null || _incomingBuffer.Length != totalSize)
            {
                IsLoading = true;
                _incomingBuffer = new byte[totalSize];
                _receivedBytesCount = 0;
                SceneManager.Instance.ResetLoadingWindow(totalSize); // Initialize progress bar based on bytes
            }

            Array.Copy(data, 0, _incomingBuffer, offset, data.Length);
            _receivedBytesCount += data.Length;

            // Update loading progress as bytes are received
            SceneManager.Instance.IncrementTransferProgress(data.Length);

            if (_receivedBytesCount >= totalSize)
            {
                ProcessDownloadedMap();
            }
        }

        private static void ProcessDownloadedMap()
        {
            try
            {
                byte[] decompressedData = DecompressData(_incomingBuffer);
                _incomingBuffer = null;
                _receivedBytesCount = 0;

                List<ObjectInfo> objects = new List<ObjectInfo>();

                using (var ms = new MemoryStream(decompressedData))
                using (var reader = new BinaryReader(ms))
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var obj = new ObjectInfo
                        {
                            objectId = reader.ReadInt32(),
                            placeIndex = reader.ReadInt32()
                        };
                        int len = reader.ReadInt32();
                        obj.componentData = reader.ReadBytes(len);
                        objects.Add(obj);
                    }
                }

                _pendingLoadObjects.Clear();
                _pendingLoadObjects.AddRange(objects);
                MonoBehaviourSingleton<BlEditorManager>.Instance.StartCoroutine(ProcessPendingLoadQueue(objects.Count));
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError($"[Client] Error processing map: {ex.Message}");
                _incomingBuffer = null;
                IsLoading = false;
            }
        }

        private static IEnumerator ProcessPendingLoadQueue(int totalCount)
        {
            if (_isProcessingLoad)
            {
                yield break;
            }

            int processed = 0;
            var stopwatch = new System.Diagnostics.Stopwatch();
            SceneManager.Instance.ResetLoadingWindow(totalCount);

            while (_pendingLoadObjects.Count > 0)
            {
                stopwatch.Restart();

                while (_pendingLoadObjects.Count > 0 && stopwatch.ElapsedMilliseconds < 15)
                {
                    var obj = _pendingLoadObjects[0];
                    _pendingLoadObjects.RemoveAt(0);

                    try
                    {
                        string json = Encoding.UTF8.GetString(obj.componentData);
                        var baseParams = JsonUtility.FromJson<BaseParameters>(json);

                        if (baseParams != null)
                        {
                            if (SceneManager.Instance.TryGetPrefab(baseParams.index, out GameObject prefab))
                            {
                                GameObject instance;
                                BaseParameters finalParams = baseParams;

                                if (prefab.TryGetComponent<Serialized>(out var component))
                                {
                                    Type specificType = component.Parameters.GetType();
                                    finalParams = (BaseParameters)JsonUtility.FromJson(json, specificType);
                                }

                                // Ensure the Place container exists before instantiation
                                if (finalParams.placeIndex >= 0)
                                {
                                    SceneManager.Instance.SetMinPlaces(finalParams.placeIndex);
                                }

                                instance = SceneManager.Instance.InstantiateEditor(prefab, finalParams.placeIndex, finalParams.position, finalParams.rotation);
                                finalParams.UpdateObject(instance.transform);

                                var netObj = instance.AddComponent<NetworkObject>();
                                netObj.NetworkId = obj.objectId;
                                WorldBuilderCoop.Core.networkObjectManager.AddNetworkObject(netObj);
                                SceneManager.Instance.IncrementTransferProgress(1);
                            }
                            else
                            {
                                CreatePlaceholder(obj.placeIndex);
                            }
                        }
                        else
                        {
                            CreatePlaceholder(obj.placeIndex);
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleBase.WriteError($"Error instantiating object {obj.objectId}: {ex.Message}");
                        CreatePlaceholder(obj.placeIndex);
                    }
                    processed++;
                }
                yield return null;
            }

            // Post-process map to resolve linked references (Doors, Waypoints, etc.)
            SceneManager.Instance.ProcessMap();
            yield return new WaitForSeconds(0.05f);
            SceneManager.Instance.SetPlace(1);
            yield return new WaitForSeconds(0.05f);
            SceneManager.Instance.SetPlace(0);
            
            if (SteamNetworkManager.Instance != null)
            {
                SteamNetworkManager.Instance.OnMapLoadCompleted();
            }

            IsLoading = false;
            PacketHandler.ProcessBufferedPackets();

            SendLoadFinished(processed);
        }

        private static void CreatePlaceholder(int placeIndex)
        {
            if (placeIndex >= 0)
            {
                SceneManager.Instance.SetMinPlaces(placeIndex);
                var nullObj = new GameObject("NullObject");
                nullObj.transform.SetParent(SceneManager.Instance.transform.GetChild(placeIndex));
                nullObj.transform.localPosition = Vector3.zero;
                nullObj.transform.localRotation = Quaternion.identity;
            }
        }

        private static void SendLoadFinished(int count)
        {
            if (SteamNetworkManager.Instance != null)
            {
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.LoadMapFinished);
                    writer.Write(GetCurrentUserId());
                    writer.Write(count);
                    SteamNetworkManager.Instance.SendToAll(ms.ToArray());
                }
            }
        }

        public static IEnumerator SendMapToClients(List<ObjectInfo> objects)
        {
            byte[] rawBytes;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(objects.Count);
                foreach (var obj in objects)
                {
                    writer.Write(obj.objectId);
                    writer.Write(obj.placeIndex);
                    writer.Write(obj.componentData.Length);
                    writer.Write(obj.componentData);
                }
                rawBytes = ms.ToArray();
            }

            byte[] compressedBytes;
            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzip.Write(rawBytes, 0, rawBytes.Length);
                }
                compressedBytes = ms.ToArray();
            }

            int chunkSize = 4096;
            int totalSize = compressedBytes.Length;
            int offset = 0;
            int maxBytesPerSecond = 200000;

            var stopwatch = new System.Diagnostics.Stopwatch();
            float startTime = Time.realtimeSinceStartup;
            int totalBytesSent = 0;

            while (offset < totalSize)
            {
                stopwatch.Restart();
                float elapsedTime = Time.realtimeSinceStartup - startTime;

                if (elapsedTime > 0.1f)
                {
                    float currentRate = totalBytesSent / elapsedTime;
                    if (currentRate > maxBytesPerSecond)
                    {
                        yield return null;
                        continue;
                    }
                }

                int remaining = totalSize - offset;
                int currentChunkSize = Mathf.Min(remaining, chunkSize);

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.LoadMap);
                    writer.Write(totalSize);
                    writer.Write(offset);
                    writer.Write(currentChunkSize);
                    writer.Write(compressedBytes, offset, currentChunkSize);
                    SteamNetworkManager.Instance.SendToAll(ms.ToArray());
                }

                offset += currentChunkSize;
                totalBytesSent += currentChunkSize;

                if (stopwatch.ElapsedMilliseconds > 10)
                {
                    yield return null;
                }
            }
        }

        private static int GetCurrentUserId()
        {
            return SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsLocalMode()
                ? LocalUserManager.GetLocalUserId()
                : Steamworks.SteamUser.GetSteamID().m_SteamID.GetHashCode();
        }

        public static List<ObjectInfo> SerializeLevel(bool checkSave)
        {
            List<ObjectInfo> list = new List<ObjectInfo>();
            List<Transform> list2;
            if (checkSave)
            {
                list2 = new List<Transform>();
                float farClipPlane = MonoBehaviourSingleton<BlSceneCamera>.Instance.mCamera.farClipPlane;
                farClipPlane *= farClipPlane;
                foreach (Transform item in SceneManager.Instance.AllTransforms())
                {
                    if (item.name == "NullObject" || item.position.sqrMagnitude >= farClipPlane)
                    {
                        item.SetParent(null);
                        UnityEngine.Object.Destroy(item.gameObject);
                    }
                    else
                    {
                        list2.Add(item);
                    }
                }
                MonoBehaviourSingleton<BlEditorManager>.Instance.ClearSelection();
            }
            else
            {
                list2 = SceneManager.Instance.AllTransforms();
            }

            // Sort to ensure hierarchy order (Place -> Sibling) for correct index preservation
            list2.Sort((a, b) =>
            {
                int placeA = a.parent ? a.parent.GetSiblingIndex() : -1;
                int placeB = b.parent ? b.parent.GetSiblingIndex() : -1;
                int comparePlace = placeA.CompareTo(placeB);
                return comparePlace != 0 ? comparePlace : a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
            });

            foreach (Transform item2 in list2)
            {
                if (!item2.TryGetComponent<NetworkObject>(out var networkObject))
                {
                    networkObject = item2.gameObject.AddComponent<NetworkObject>();
                    WorldBuilderCoop.Core.networkObjectManager.AddNetworkObject(networkObject);
                }

                byte[] data;
                int savedPlaceIndex;

                if (item2.TryGetComponent<Serialized>(out var component))
                {
                    component.CheckSave();
                    savedPlaceIndex = component.Parameters.placeIndex;
                    data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(component.Parameters));
                }
                else
                {
                    var bp = new BaseParameters(item2);
                    savedPlaceIndex = bp.placeIndex;
                    data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(bp));
                }

                list.Add(new ObjectInfo
                {
                    objectId = networkObject.NetworkId,
                    placeIndex = savedPlaceIndex,
                    componentData = data
                });
            }
            return list;
        }

        public static byte[] DecompressData(byte[] compressedData)
        {
            using (var compressedMs = new MemoryStream(compressedData))
            using (var decompressedMs = new MemoryStream())
            using (var gzip = new GZipStream(compressedMs, CompressionMode.Decompress))
            {
                gzip.CopyTo(decompressedMs);
                return decompressedMs.ToArray();
            }
        }
    }
}
