using BrokeProtocol.Managers;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldBuilderCoop.UI
{
    internal class ConnectedUI
    {
        public static void createDisconnectBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button disconnectBtn = new Button { text = "Disconnect", name = "Disconnect" };

            Styles.ApplyButtonStyle(disconnectBtn);

            disconnectBtn.style.color = Color.red;

            disconnectBtn.clicked += () => disconnect(sceneManager);
            root.Add(disconnectBtn);
        }

        public static void destroyDisconnectBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var disconnectBtn = root.Q<Button>("Disconnect");

            if (disconnectBtn != null)
            {
                root.Remove(disconnectBtn);
            }
        }

        public static void disconnect(SceneManager sceneManager)
        {
            Core.Network.Disconnect();
            destroyDisconnectBtn(sceneManager);
            MainUI.createHostBtn(sceneManager);
            MainUI.createJoinBtn(sceneManager);
        }
    }
}
