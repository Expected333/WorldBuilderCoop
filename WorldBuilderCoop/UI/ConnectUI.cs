using BrokeProtocol.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldBuilderCoop.UI
{
    internal class ConnectUI
    {
        public static bool isConnectUIExist = false;
        public static void createConnectUI(SceneManager sceneManager)
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
            Styles.ApplyInputStyle(ipInput);

            TextField portInput = new TextField { label = "PORT", name = "PortInput", value = "7777" };
            Styles.ApplyInputStyle(portInput);

            Button connectBtn = new Button { text = "CONNECT", name = "ConnectButton" };
            Styles.ApplyButtonStyle(connectBtn);

            connectBtn.clicked += () => connectSession(sceneManager, ipInput.value, portInput.value);

            container.Add(ipInput);
            container.Add(portInput);
            container.Add(connectBtn);
            root.Add(container);
            isConnectUIExist = true;
        }

        public static void destroyConnectUI(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var container = root.Q<VisualElement>("ConnectPanel");

            if (container != null)
            {
                root.Remove(container);
                isConnectUIExist = false;
            }
        }

        public static void connectSession(SceneManager sceneManager, string ip, string portStr)
        {
            if (ushort.TryParse(portStr, out ushort port))
            {
                destroyConnectUI(sceneManager);
                MainUI.destroyHostAndJoinUI(sceneManager);
                Core.Network.JoinAsClient(ip, port);
                ConnectedUI.createDisconnectBtn(sceneManager);
            }
        }
    }
}
