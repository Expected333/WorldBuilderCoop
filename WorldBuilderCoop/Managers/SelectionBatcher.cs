using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Network;
using WorldBuilderCoop.Events;
using ModLoader;
using System.IO;
using Steamworks;

namespace WorldBuilderCoop.Managers
{
    public class SelectionBatcher : MonoBehaviour
    {
        public static SelectionBatcher Instance { get; private set; }

        private HashSet<long> _pendingSelectionAdds = new HashSet<long>();
        // Objets dont les paramètres ont changé (édition d'inspecteur) à répliquer.
        private HashSet<long> _pendingComponentSyncs = new HashSet<long>();
        private bool _batchingEnabled = true;

        private void Awake()
        {
            Instance = this;
        }

        public void RequestSelection(long networkId)
        {
            if (!_batchingEnabled) return;
            _pendingSelectionAdds.Add(networkId);
        }

        /// <summary>Annule une sélection encore en attente (objet désélectionné avant le flush du batch).</summary>
        public void CancelPendingSelection(long networkId)
        {
            _pendingSelectionAdds.Remove(networkId);
        }

        /// <summary>Marque un objet comme "paramètres modifiés" ; l'état le plus récent
        /// sera sérialisé et envoyé une fois par frame (coalescence des éditions rapides).</summary>
        public void RequestComponentSync(long networkId)
        {
            if (!_batchingEnabled) return;
            _pendingComponentSyncs.Add(networkId);
        }

        public void RequestDuplicate(List<DuplicateEntry> entries)
        {
            int userId = GetCurrentUserId();
            SendDuplicateSelectionPacket(userId, entries);
        }

        private void Update()
        {
            if (_pendingSelectionAdds.Count > 0)
            {
                SendBatchSelection();
            }
            if (_pendingComponentSyncs.Count > 0)
            {
                SendComponentSyncs();
            }
        }

        private void SendComponentSyncs()
        {
            // Copie puis vide : on lit l'état COURANT (le plus récent) de chaque objet.
            List<long> ids = new List<long>(_pendingComponentSyncs);
            _pendingComponentSyncs.Clear();

            foreach (long id in ids)
            {
                try
                {
                    NetworkObject netObj = Core.networkObjectManager.GetNetworkObject(id);
                    if (netObj == null || netObj.transform == null) continue;

                    string json = MapManager.SerializeObjectParametersJson(netObj.transform);
                    if (string.IsNullOrEmpty(json)) continue;

                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)Packets.UpdateComponent);
                        writer.Write(id);
                        writer.Write(json);
                        SendPacket(ms.ToArray());
                    }
                }
                catch (System.Exception ex)
                {
                    ConsoleBase.WriteError($"Error sending component sync for {id}: {ex.Message}");
                }
            }
        }

        private void SendBatchSelection()
        {
            int userId = GetCurrentUserId();
            List<long> objectIds = new List<long>(_pendingSelectionAdds);
            _pendingSelectionAdds.Clear();

            // Met à jour l'état local puis envoie UN paquet batch.
            // NE PAS appeler RaiseSelectionChanged ici : ça enverrait un second paquet
            // (AddToSelection unitaire) en plus du batch → doublon + boucle d'écho.
            foreach (long id in objectIds)
            {
                Core.networkObjectManager.MarkAsSelectedByUser(userId, id);
            }

            // Send Packet
            try
            {
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)Packets.BatchAddToSelection);
                        writer.Write(userId);
                        writer.Write(objectIds.Count);
                        foreach (long id in objectIds)
                        {
                            writer.Write(id);
                        }
                        
                        byte[] data = ms.ToArray();
                        SendPacket(data);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError($"Error sending batch selection: {ex.Message}");
            }
        }

        private void SendDuplicateSelectionPacket(int userId, List<DuplicateEntry> entries)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)Packets.DuplicateSelection);
                        writer.Write(userId);
                        writer.Write(entries.Count);
                        foreach (var e in entries)
                        {
                            writer.Write(e.sourceId);
                            writer.Write(e.newId);
                            writer.Write(e.position.x);
                            writer.Write(e.position.y);
                            writer.Write(e.position.z);
                            writer.Write(e.rotation.x);
                            writer.Write(e.rotation.y);
                            writer.Write(e.rotation.z);
                            writer.Write(e.rotation.w);
                            writer.Write(e.scale.x);
                            writer.Write(e.scale.y);
                            writer.Write(e.scale.z);
                        }

                        byte[] data = ms.ToArray();
                        SendPacket(data);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError($"Error sending duplicate selection: {ex.Message}");
            }
        }

        private void SendPacket(byte[] data)
        {
            if (SteamNetworkManager.Instance != null)
            {
                SteamNetworkManager.Instance.SendToAll(data);
            }
        }

        private int GetCurrentUserId() => NetworkIdentity.GetUserId();
    }
}
