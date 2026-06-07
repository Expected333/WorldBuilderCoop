using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using ModLoader;
using UnityEngine;
using UnityEngine.UIElements;
using WorldBuilderCoop.UI;

namespace WorldBuilderCoop
{
    internal class UIHosting
    {
        public static void ApplyBlEditorTheme()
        {
            var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
            if (sceneManager == null)
            {
                WbLog.Debug("[WBCoop] SceneManager not found");
                return;
            }

            // Get root using UIDocument
            var allUIDocuments = UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
            VisualElement root = null;

            foreach (var doc in allUIDocuments)
            {
                if (doc.rootVisualElement != null && doc.rootVisualElement.childCount > 0)
                {
                    // Verify it's the Builder UI by checking for known elements
                    var testElement = doc.rootVisualElement.Q("Background");
                    if (testElement != null)
                    {
                        root = doc.rootVisualElement;
                        WbLog.Debug($"[WBCoop] Found Builder UI with {root.childCount} children");
                        break;
                    }
                }
            }

            if (root == null)
            {
                WbLog.Debug("[WBCoop] Could not find Builder UI root");
                return;
            }

            // Show mode selection first
            MainUI.createModeSelectionUI(sceneManager);

            WbLog.Debug("[WBCoop] Builder theme applied successfully");
        }
    }
}