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

using Steamworks;
using System.Net.Sockets;

namespace WorldBuilderCoop.Managers
{
    public static class MapManager
    {
        private static byte[] _incomingBuffer;
        private static int _receivedBytesCount;
        // Suivi des chunks reçus (par index) : dédoublonnage + détection de complétude
        // déterministe (indépendante du compte d'octets, robuste aux doublons/réordonnancements).
        private static readonly HashSet<int> _receivedChunks = new HashSet<int>();
        private static int _expectedChunks = -1;
        private static bool _mapComplete;
        private static readonly List<ObjectInfo> _pendingLoadObjects = new List<ObjectInfo>();
        private static readonly bool _isProcessingLoad = false;

        // Taille de chunk : gros chunks => bien moins de paquets => transfert plus fiable/rapide.
        // Reste sous NetworkConfig.MaxPacketSize (1 Mo) et sous la limite Steam P2P fiable (1 Mo).
        private const int MapChunkSize = 512 * 1024;

        // Quand true, BlEditorManager.AppendHistory est court-circuité (cf. patch dédié).
        // L'historique = un snapshot JSON de TOUTE la scène ; le déclencher par objet pendant
        // un chargement/replay de map donne un coût O(N²) (gel + OOM sur grosses maps).
        public static bool SuppressHistory = false;

        private static bool _isLoading = false;
        public static bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    /*
                    if (_isLoading)
                    {
                        // Enable loading screen for everyone when sync starts
                        // SceneManager.Instance.ResetLoadingWindow(1); // Placeholder
                        BlEditorManager.Instance.ClearSelection();
                        MonoBehaviourSingleton<BrokeProtocol.Managers.SceneManager>.Instance.Clear();
                    }
                    else
                    {
                        // Hide loading screen
                        SceneManager.Instance.ResetLoadingWindow(0);
                    }
                    */
                }
            }
        }
        
        public static bool IsSyncing { get; private set; }

        public static void SetSyncState(bool syncing, bool clearMap = true, int totalSize = 0)
        {
            IsSyncing = syncing;
            // IsLoading = syncing && clearMap;
        }

        public static void FinishLoading()
        {
            SetSyncState(false);
        }

        public static void HandleIncomingChunk(int totalSize, int totalChunks, int chunkIndex, int offset, byte[] data)
        {
            // Nouveau transfert détecté (taille ou nombre de chunks différent, ou pas de buffer).
            if (_incomingBuffer == null || _incomingBuffer.Length != totalSize || _expectedChunks != totalChunks)
            {
                _incomingBuffer = new byte[totalSize];
                _receivedBytesCount = 0;
                _receivedChunks.Clear();
                _expectedChunks = totalChunks;
                _mapComplete = false;
                // Garde-fou : repart toujours d'un historique actif (au cas où un chargement
                // précédent aurait été interrompu en laissant le flag à true).
                SuppressHistory = false;

                BlEditorManager.Instance.AppendHistory();
                BlEditorManager.Instance.ClearSelection();
                MonoBehaviourSingleton<BrokeProtocol.Managers.SceneManager>.Instance.Clear();
                // La scène est vidée : on repart d'un registre d'objets propre pour
                // réenregistrer avec les IDs autoritaires du host (les avatars sont conservés).
                WorldBuilderCoop.Core.networkObjectManager.ClearObjects();

                MapLoadingScreen.BeginDownload(totalSize);
                WbLog.Debug($"[Client] Started receiving map. Total: {totalSize} bytes, {totalChunks} chunks");
            }

            // Place le chunk à son offset. Compte les octets une seule fois par index (dédoublonnage).
            if (offset >= 0 && offset + data.Length <= _incomingBuffer.Length)
            {
                Array.Copy(data, 0, _incomingBuffer, offset, data.Length);
            }
            if (_receivedChunks.Add(chunkIndex))
            {
                _receivedBytesCount += data.Length;
            }

            MapLoadingScreen.DownloadProgress(_receivedBytesCount);

            // Complétude déterministe : tous les chunks reçus (pas un simple cumul d'octets).
            if (!_mapComplete && _expectedChunks > 0 && _receivedChunks.Count >= _expectedChunks)
            {
                _mapComplete = true;
                WbLog.Debug($"[Client] Map download complete ({_receivedChunks.Count}/{_expectedChunks} chunks). Processing...");
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
                _receivedChunks.Clear();
                _expectedChunks = -1;

                List<ObjectInfo> objects = new List<ObjectInfo>();

                using (var ms = new MemoryStream(decompressedData))
                using (var reader = new BinaryReader(ms))
                {
                    int count = reader.ReadInt32();
                    for (int i = 0; i < count; i++)
                    {
                        var obj = new ObjectInfo
                        {
                            objectId = reader.ReadInt64(),
                            placeIndex = reader.ReadInt32(),
                            prefabIndex = reader.ReadInt32()
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
                _receivedChunks.Clear();
                _expectedChunks = -1;
                MapLoadingScreen.End();
                SetSyncState(false);
                // IMPORTANT : sortir de l'état "loading" et vider la file, sinon _isLoadingMap
                // resterait true et l'invité mettrait tous les paquets en attente indéfiniment.
                SteamNetworkManager.Instance?.OnMapLoadCompleted();
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
            // SceneManager.Instance.ResetLoadingWindow(totalCount);

            int maxPlaceIndex = -1;
            for (int i = 0; i < _pendingLoadObjects.Count; i++)
            {
                if (_pendingLoadObjects[i].placeIndex > maxPlaceIndex)
                {
                    maxPlaceIndex = _pendingLoadObjects[i].placeIndex;
                }
            }
            if (maxPlaceIndex >= 0)
            {
                SceneManager.Instance.SetMinPlaces(maxPlaceIndex);
            }

            WbLog.Debug($"[Client] Processing {totalCount} objects...");
            MapLoadingScreen.BeginProcessing(totalCount);

            // Coupe les snapshots d'historique pendant l'instanciation en masse : InstantiateEditor
            // appelle AppendHistory par objet → O(N²) (gel/OOM sur grosses maps). Un chargement de
            // map n'est de toute façon pas une action annulable par objet.
            SuppressHistory = true;

            while (_pendingLoadObjects.Count > 0)
            {
                stopwatch.Restart();

                while (_pendingLoadObjects.Count > 0 && stopwatch.ElapsedMilliseconds < 15)
                {
                    var obj = _pendingLoadObjects[0];
                    _pendingLoadObjects.RemoveAt(0);

                    try
                    {
                        if (SceneManager.Instance.TryGetPrefab(obj.prefabIndex, out GameObject prefab))
                        {
                            Type type = typeof(BaseParameters);
                            if (prefab.TryGetComponent<Serialized>(out var component))
                            {
                                type = component.Parameters.GetType();
                            }

                            string json = Encoding.UTF8.GetString(obj.componentData);
                            var finalParams = (BaseParameters)JsonUtility.FromJson(json, type);

                            if (finalParams != null)
                            {
                                var instance = SceneManager.Instance.InstantiateEditor(prefab, finalParams.placeIndex, finalParams.position, finalParams.rotation);
                                finalParams.UpdateObject(instance.transform);

                                var netObj = instance.AddComponent<NetworkObject>();
                                netObj.PrefabIndex = obj.prefabIndex;
                                // Conserver l'ID autoritaire du host (ne PAS réassigner via AddNetworkObject).
                                WorldBuilderCoop.Core.networkObjectManager.RegisterNetworkObject(obj.objectId, netObj);
                                // SceneManager.Instance.IncrementTransferProgress(1);
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
                MapLoadingScreen.ProcessingProgress(processed);
                yield return null;
            }

            // Instanciation terminée : on réactive l'historique normal.
            SuppressHistory = false;

            // Post-process map for entities
            SceneManager.Instance.ProcessMap();
            yield return new WaitForSeconds(0.05f);
            SceneManager.Instance.SetPlace(1);
            yield return new WaitForSeconds(0.05f);
            SceneManager.Instance.SetPlace(0);

            if (SteamNetworkManager.Instance != null)
            {
                SteamNetworkManager.Instance.OnMapLoadCompleted();
            }

            SetSyncState(false);
            PacketHandler.ProcessBufferedPackets();

            MapLoadingScreen.End();
            SendLoadFinished(processed);
            WbLog.Debug("[Client] Map processing finished.");
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
            byte[] compressed = SerializeAndCompress(objects);
            yield return SendChunks(compressed, data => SteamNetworkManager.Instance.SendToAll(data));
        }

        /// <summary>Sérialise la liste d'objets puis la compresse en gzip.</summary>
        private static byte[] SerializeAndCompress(List<ObjectInfo> objects)
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
                    writer.Write(obj.prefabIndex);
                    writer.Write(obj.componentData.Length);
                    writer.Write(obj.componentData);
                }
                rawBytes = ms.ToArray();
            }

            using (var ms = new MemoryStream())
            {
                using (var gzip = new GZipStream(ms, CompressionMode.Compress))
                {
                    gzip.Write(rawBytes, 0, rawBytes.Length);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Envoie les données compressées en chunks. Format du paquet LoadMap :
        /// [type][totalSize][totalChunks][chunkIndex][offset][chunkSize][data].
        /// Gros chunks (512 Ko) = peu de paquets = transfert fiable et rapide ; un chunk par frame.
        /// </summary>
        private static IEnumerator SendChunks(byte[] compressed, Action<byte[]> send)
        {
            int totalSize = compressed.Length;
            int totalChunks = Mathf.Max(1, (totalSize + MapChunkSize - 1) / MapChunkSize);
            int offset = 0;

            for (int index = 0; index < totalChunks; index++)
            {
                int currentChunkSize = Mathf.Min(MapChunkSize, totalSize - offset);
                if (currentChunkSize < 0) currentChunkSize = 0;

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)Packets.LoadMap);
                    writer.Write(totalSize);
                    writer.Write(totalChunks);
                    writer.Write(index);
                    writer.Write(offset);
                    writer.Write(currentChunkSize);
                    if (currentChunkSize > 0) writer.Write(compressed, offset, currentChunkSize);
                    send(ms.ToArray());
                }

                offset += currentChunkSize;
                yield return null; // un chunk par frame pour ne pas figer le jeu
            }
        }

        private static int GetCurrentUserId() => NetworkIdentity.GetUserId();

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
                int prefabIndex;

                if (item2.TryGetComponent<Serialized>(out var component))
                {
                    component.CheckSave();
                    savedPlaceIndex = component.Parameters.placeIndex;
                    prefabIndex = component.Parameters.index;
                    data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(component.Parameters));
                }
                else
                {
                    var bp = new BaseParameters(item2);
                    savedPlaceIndex = bp.placeIndex;
                    prefabIndex = bp.index;
                    data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(bp));
                }

                list.Add(new ObjectInfo
                {
                    objectId = networkObject.NetworkId,
                    placeIndex = savedPlaceIndex,
                    prefabIndex = prefabIndex,
                    componentData = data
                });
            }
            return list;
        }

        public static IEnumerator SendMapToClient(CSteamID target)
        {
            var objects = SerializeLevel(false);
            yield return SendMapToClientInternal(objects, 
                (data) => SteamNetworkManager.Instance.SendTo(target, data),
                (data) => SteamNetworkManager.Instance.SendToAllExcept(target, data)
            );
        }

        public static IEnumerator SendMapToClient(TcpClient target)
        {
            var objects = SerializeLevel(false);
            yield return SendMapToClientInternal(objects, 
                (data) => SteamNetworkManager.Instance.SendTo(target, data),
                (data) => SteamNetworkManager.Instance.SendToAllExcept(target, data)
            );
        }

        private static IEnumerator SendMapToClientInternal(List<ObjectInfo> objects, Action<byte[]> sendToTarget, Action<byte[]> sendToOthers)
        {
            WbLog.Debug("[MapManager] Starting Single Client Map Sync");
            SetSyncState(true, false);

            byte[] compressed = SerializeAndCompress(objects);
            yield return SendChunks(compressed, sendToTarget);

            SetSyncState(false);

            // Notify target finished
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)Packets.LoadMapFinished);
                writer.Write(GetCurrentUserId());
                writer.Write(objects.Count);
                sendToTarget(ms.ToArray());
            }
            WbLog.Debug("[MapManager] Single Client Map Sync Completed");
        }

        /// <summary>
        /// Sérialise en JSON l'état courant des paramètres d'un objet (transform + champs custom),
        /// en suivant exactement le chemin de SerializeLevel.
        /// </summary>
        public static string SerializeObjectParametersJson(Transform t)
        {
            if (t.TryGetComponent<Serialized>(out var component))
            {
                component.CheckSave();
                return JsonUtility.ToJson(component.Parameters);
            }
            return JsonUtility.ToJson(new BaseParameters(t));
        }

        /// <summary>
        /// Applique un blob JSON de paramètres à un objet existant (réplication d'une édition d'inspecteur).
        /// Réutilise BaseParameters.UpdateObject, qui gère les champs custom via ses overrides.
        /// </summary>
        public static void ApplyObjectParametersJson(Transform t, string json)
        {
            if (t == null || string.IsNullOrEmpty(json)) return;

            Type type = typeof(BaseParameters);
            if (t.TryGetComponent<Serialized>(out var component))
            {
                type = component.Parameters.GetType();
            }

            var finalParams = (BaseParameters)JsonUtility.FromJson(json, type);
            if (finalParams == null) return;

            finalParams.UpdateObject(t);

            // Aligner l'interpolateur (s'il existe) pour éviter qu'il ne "ramène" l'objet
            // vers une cible de déplacement antérieure.
            var interpolator = t.GetComponent<WorldBuilderCoop.GameObjectInterpolator>();
            if (interpolator != null)
            {
                interpolator.SetTarget(t.position, t.rotation, t.localScale);
            }
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
