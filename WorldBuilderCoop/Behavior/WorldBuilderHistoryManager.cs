using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BrokeProtocol.Managers;
using BrokeProtocol.Parameters;
using BrokeProtocol.Utility;
using ModLoader;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop.Behavior
{
    /// <summary>
    /// Préserve les NetworkId à travers l'undo/redo.
    ///
    /// L'undo du jeu reconstruit toute la scène (Clear + DeserializeLevel) à partir d'un
    /// snapshot (List&lt;BaseParameters&gt;) : les NetworkObject (ajoutés par le mod) sont perdus.
    ///
    /// Approche : on associe chaque snapshot à la liste ORDONNÉE des NetworkId, capturée dans
    /// l'ordre AllTransforms() — exactement l'ordre dans lequel le snapshot a été sérialisé et
    /// dans lequel DeserializeLevel recrée les objets. La restauration se fait donc par INDEX
    /// (déterministe, exact), et non par appariement flou nom+position.
    ///
    /// La ConditionalWeakTable est indexée par référence de snapshot : quand le jeu retire un
    /// snapshot de son historique, l'entrée est automatiquement collectée par le GC.
    /// </summary>
    public class WorldBuilderHistoryManager : MonoBehaviour
    {
        public static WorldBuilderHistoryManager Instance;

        private readonly ConditionalWeakTable<object, List<long>> _idsBySnapshot
            = new ConditionalWeakTable<object, List<long>>();

        private void Awake() => Instance = this;

        /// <summary>
        /// Capture les NetworkId courants (ordre AllTransforms) et les associe au snapshot
        /// qui vient d'être produit par SceneManager.SerializeLevel(checkSave:false).
        /// </summary>
        public void AssociateSnapshot(List<BaseParameters> snapshot)
        {
            if (snapshot == null) return;
            var sm = MonoBehaviourSingleton<SceneManager>.Instance;
            if (sm == null) return;

            var ids = new List<long>();
            foreach (Transform t in sm.AllTransforms())
            {
                if (!t.TryGetComponent<NetworkObject>(out var netObj))
                {
                    netObj = t.gameObject.AddComponent<NetworkObject>();
                    Core.networkObjectManager.AddNetworkObject(netObj); // alloue un id
                }
                ids.Add(netObj.NetworkId);
            }

            _idsBySnapshot.Remove(snapshot);
            _idsBySnapshot.Add(snapshot, ids);
        }

        /// <summary>
        /// Réassigne les NetworkId par index après qu'un snapshot a été restauré (undo/redo).
        /// </summary>
        public void RestoreSnapshot(List<BaseParameters> snapshot)
        {
            if (snapshot == null) return;

            if (!_idsBySnapshot.TryGetValue(snapshot, out var ids))
            {
                ConsoleBase.WriteError("[History] Snapshot non associé : NetworkId non restaurés (désync possible)");
                return;
            }

            var sm = MonoBehaviourSingleton<SceneManager>.Instance;
            if (sm == null) return;

            var transforms = sm.AllTransforms();
            if (transforms.Count != ids.Count)
            {
                ConsoleBase.WriteError($"[History] Restore: {transforms.Count} objets vs {ids.Count} ids — appariement partiel");
            }

            int n = Mathf.Min(transforms.Count, ids.Count);
            for (int i = 0; i < n; i++)
            {
                Transform t = transforms[i];
                if (!t.TryGetComponent<NetworkObject>(out var netObj))
                {
                    netObj = t.gameObject.AddComponent<NetworkObject>();
                }
                Core.networkObjectManager.RegisterNetworkObject(ids[i], netObj);
            }
        }
    }
}
