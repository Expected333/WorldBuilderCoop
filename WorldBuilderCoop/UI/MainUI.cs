using BrokeProtocol.Managers;
using UnityEngine.UIElements;

namespace WorldBuilderCoop.UI
{
    internal class MainUI
    {
        public static void createJoinBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button joinBtn = new Button { text = "JOIN GAME", name = "JoinButton" };

            Styles.ApplyButtonStyle(joinBtn);

            joinBtn.clicked += () => ConnectUI.createConnectUI(sceneManager);
            root.Add(joinBtn);
        }

        public static void createHostBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            Button hostBtn = new Button { text = "HOST GAME", name = "HostButton" };

            Styles.ApplyButtonStyle(hostBtn);

            hostBtn.clicked += () => createHost(sceneManager);
            root.Add(hostBtn);
        }

        public static void destroyHostAndJoinUI(SceneManager sceneManager)
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

        public static void createHost(SceneManager sceneManager)
        {
            destroyHostAndJoinUI(sceneManager);
            Core.Network.CreateHost();
            ConnectedUI.createDisconnectBtn(sceneManager);
        }
    }
}
