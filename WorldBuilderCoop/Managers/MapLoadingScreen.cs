using System.Reflection;
using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using UnityEngine;

namespace WorldBuilderCoop.Managers
{
    /// <summary>
    /// Pilote l'écran de chargement natif de BROKE PROTOCOL (LoadingWindow) pendant la
    /// réception + le traitement de la map côté client — qui sinon n'affiche aucun écran.
    ///
    /// Barre en 2 segments : segment 1 = téléchargement (octets), segment 2 = instanciation
    /// (objets). On contrôle loadTotal/loadProgress (publics) et segmentTotal/segmentProgress
    /// (privés, via réflexion) pour que SceneManager.LoadedFraction donne un vrai 0→100%.
    /// </summary>
    public static class MapLoadingScreen
    {
        private static FieldInfo _segTotalField;
        private static FieldInfo _segProgressField;

        private static SceneManager SM => MonoBehaviourSingleton<SceneManager>.Instance;

        private static void EnsureReflection()
        {
            if (_segTotalField == null)
            {
                _segTotalField = typeof(SceneManager).GetField("segmentTotal", BindingFlags.NonPublic | BindingFlags.Instance);
                _segProgressField = typeof(SceneManager).GetField("segmentProgress", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        private static void SetSegments(SceneManager sm, int total, int progress)
        {
            EnsureReflection();
            _segTotalField?.SetValue(sm, total);
            _segProgressField?.SetValue(sm, progress);
        }

        /// <summary>Affiche l'écran et démarre le segment "téléchargement".</summary>
        public static void BeginDownload(int totalBytes)
        {
            var sm = SM;
            if (sm == null) return;
            try
            {
                sm.ShowLoadingWindow();
                SetSegments(sm, 2, 1);
                sm.loadTotal = Mathf.Max(1, totalBytes);
                sm.loadProgress = 0;
                sm.loadingWindow?.Refresh();
            }
            catch (System.Exception ex) { WbLog.Error("[MapLoadingScreen] BeginDownload: " + ex.Message); }
        }

        public static void DownloadProgress(int receivedBytes)
        {
            var sm = SM;
            if (sm == null || sm.loadingWindow == null) return;
            sm.loadProgress = receivedBytes;
            sm.loadingWindow.Refresh();
        }

        /// <summary>Passe au segment "instanciation des objets".</summary>
        public static void BeginProcessing(int totalObjects)
        {
            var sm = SM;
            if (sm == null) return;
            try
            {
                if (sm.loadingWindow == null) sm.ShowLoadingWindow();
                SetSegments(sm, 2, 2);
                sm.loadTotal = Mathf.Max(1, totalObjects);
                sm.loadProgress = 0;
                sm.loadingWindow?.Refresh();
            }
            catch (System.Exception ex) { WbLog.Error("[MapLoadingScreen] BeginProcessing: " + ex.Message); }
        }

        public static void ProcessingProgress(int processed)
        {
            var sm = SM;
            if (sm == null || sm.loadingWindow == null) return;
            sm.loadProgress = processed;
            sm.loadingWindow.Refresh();
        }

        /// <summary>Ferme l'écran de chargement.</summary>
        public static void End()
        {
            var sm = SM;
            if (sm == null || sm.loadingWindow == null) return;
            try
            {
                sm.loadingWindow.Destroy();
                sm.loadingWindow = null;
            }
            catch (System.Exception ex) { WbLog.Error("[MapLoadingScreen] End: " + ex.Message); }
        }
    }
}
