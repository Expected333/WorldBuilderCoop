using BrokeProtocol.Managers;
using BrokeProtocol.Utility;
using ModLoader;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldBuilderCoop
{
    internal class UIHosting
    {
        private static bool isConnectUIExist = false;

        public static void ApplyBlEditorTheme()
        {
            var sceneManager = MonoBehaviourSingleton<SceneManager>.Instance;
            if (sceneManager == null)
            {
                ConsoleBase.WriteLine("[WBCoop] SceneManager not found");
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
                        ConsoleBase.WriteLine($"[WBCoop] Found Builder UI with {root.childCount} children");
                        break;
                    }
                }
            }

            if (root == null)
            {
                ConsoleBase.WriteLine("[WBCoop] Could not find Builder UI root");
                return;
            }

            createHostBtn(sceneManager);
            createJoinBtn(sceneManager);

            ConsoleBase.WriteLine("[WBCoop] Builder theme applied successfully");
        }

        private static void createHostBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button hostBtn = new Button { text = "HOST GAME", name = "HostButton" };

            ApplyButtonStyle(hostBtn);

            hostBtn.clicked += () => createHost(sceneManager);
            root.Add(hostBtn);
        }

        private static void createJoinBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button joinBtn = new Button { text = "JOIN GAME", name = "JoinButton" };

            ApplyButtonStyle(joinBtn);

            joinBtn.clicked += () => createConnectUI(sceneManager);
            root.Add(joinBtn);
        }

        private static void createHost(SceneManager sceneManager)
        {
            destroyHostAndConnectUI(sceneManager);
            Core.Network.CreateHost();
            createDisconnectBtn(sceneManager);
        }

        private static void createConnectUI(SceneManager sceneManager)
        {
            if (isConnectUIExist) return;
            var root = sceneManager.uiDocument.rootVisualElement;

            VisualElement container = new VisualElement();
            container.name = "ConnectPanel";

            container.style.paddingLeft = container.style.paddingRight = 20;
            container.style.paddingTop = container.style.paddingBottom = 20;
            container.style.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.8f);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 10;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 10;
            container.style.width = 260;

            TextField ipInput = new TextField { label = "IP ADDRESS", name = "IPInput", value = "127.0.0.1" };
            ApplyInputStyle(ipInput);

            TextField portInput = new TextField { label = "PORT", name = "PortInput", value = "7777" };
            ApplyInputStyle(portInput);

            Button connectBtn = new Button { text = "CONNECT", name = "ConnectButton" };
            ApplyButtonStyle(connectBtn);

            connectBtn.clicked += () => connectSession(sceneManager, ipInput.value, portInput.value);

            container.Add(ipInput);
            container.Add(portInput);
            container.Add(connectBtn);
            root.Add(container);
            isConnectUIExist = true;
        }

        private static void connectSession(SceneManager sceneManager, string ip, string portStr)
        {
            if (ushort.TryParse(portStr, out ushort port))
            {
                destroyConnectUI(sceneManager);
                destroyHostAndConnectUI(sceneManager);
                Core.Network.JoinAsClient(ip, port);
                createDisconnectUI(sceneManager);
            }
        }

        private static void createDisconnectUI(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button disconnectBtn = new Button { text = "Disconnect", name = "Disconnect" };

            ApplyButtonStyle(disconnectBtn);

            disconnectBtn.style.color = Color.red;

            disconnectBtn.clicked += () => disconnect(sceneManager);
            root.Add(disconnectBtn);
        }

        private static void disconnect(SceneManager sceneManager)
        {
            Core.Network.Disconnect();
            destroyDisconnectBtn(sceneManager);
            createHostBtn(sceneManager);
            createJoinBtn(sceneManager);
        }

        private static void destroyDisconnectBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var disconnectBtn = root.Q<Button>("Disconnect");

            if (disconnectBtn != null)
            {
                root.Remove(disconnectBtn);
            }
        }

        private static void destroyHostAndConnectUI(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var HostButton = root.Q<Button>("HostButton");
            var JoinButton = root.Q<Button>("JoinButton");

            if (HostButton != null && JoinButton != null)
            {
                root.Remove(HostButton);
                root.Remove(JoinButton);
            }
        }

        private static void destroyConnectUI(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var container = root.Q<VisualElement>("ConnectPanel");

            if (container != null)
            {
                root.Remove(container);
                isConnectUIExist = false;
            }
        }

        private static void ApplyInputStyle(TextField field)
        {
            var s = field.style;
            s.marginBottom = 10;
            s.color = Color.white;
            s.unityFontStyleAndWeight = FontStyle.Bold;

            var label = field.Q<Label>();
            if (label != null)
            {
                label.style.color = new Color(0.15f, 0.67f, 0.95f, 1f);
                label.style.fontSize = 12;
                label.style.marginBottom = 2;
            }

            var input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                input.style.borderLeftColor = input.style.borderRightColor = input.style.borderTopColor = input.style.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 0.5f);
                input.style.borderLeftWidth = input.style.borderRightWidth = input.style.borderTopWidth = input.style.borderBottomWidth = 1;
                input.style.paddingLeft = 5;
                input.style.height = 30;
                input.style.color = Color.white;
                input.style.fontSize = 14;
            }
        }

        private static void ApplyButtonStyle(Button btn)
        {
            var s = btn.style;

            s.width = 220;
            s.height = 60;
            s.fontSize = 18;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.color = Color.white;
            s.marginTop = s.marginBottom = 8;

            s.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 1f);
            s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = s.borderBottomWidth = 2;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 5;

            s.transitionDuration = new List<TimeValue> { new TimeValue(100, TimeUnit.Millisecond) };
            s.transitionProperty = new List<StylePropertyName> {
                new StylePropertyName("background-color"),
                new StylePropertyName("scale"),
                new StylePropertyName("border-color")
            };

            btn.RegisterCallback<MouseEnterEvent>(evt =>
            {
                s.backgroundColor = new Color(0.15f, 0.67f, 0.95f, 0.4f);
                s.scale = new Scale(new Vector2(1.05f, 1.05f));
                s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = Color.white;
            });

            btn.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                s.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
                s.scale = new Scale(new Vector2(1f, 1f));
                s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = new Color(0.15f, 0.67f, 0.95f, 1f);
            });

            btn.RegisterCallback<MouseDownEvent>(evt =>
            {
                s.backgroundColor = Color.white;
                s.color = Color.black;
                s.scale = new Scale(new Vector2(0.92f, 0.92f));
            });

            btn.RegisterCallback<MouseUpEvent>(evt =>
            {
                s.backgroundColor = new Color(0.15f, 0.67f, 0.95f, 0.4f);
                s.color = Color.white;
                s.scale = new Scale(new Vector2(1.05f, 1.05f));
            });
        }
    }
}
