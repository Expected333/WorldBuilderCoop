using BrokeProtocol.Managers;
using ModLoader;
using Steamworks;
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

            Label infoLabel = new Label { text = "Ask the host for their Lobby ID" };
            infoLabel.style.fontSize = 10;
            infoLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            infoLabel.style.marginTop = -15;
            infoLabel.style.marginBottom = 10;

            TextField inputField = new TextField { label = "LOBBY ID", name = "LobbyIdInput", value = "" };
            Styles.ApplyInputStyle(inputField);

            Button connectBtn = new Button { text = "JOIN", name = "ConnectButton" };
            Styles.ApplyButtonStyle(connectBtn);
            connectBtn.clicked += () => connectSession(sceneManager, inputField.value);

            container.Add(infoLabel);
            container.Add(inputField);
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

        public static void connectSession(SceneManager sceneManager, string inputValue)
        {
            destroyConnectUI(sceneManager);
            MainUI.destroyHostAndJoinUI(sceneManager);

            if (string.IsNullOrEmpty(inputValue))
            {
                ConsoleBase.WriteError("[WorldBuilder] Lobby ID cannot be empty");
                return;
            }

            if (ulong.TryParse(inputValue, out ulong lobbyIdValue))
            {
                CSteamID lobbyId = new CSteamID(lobbyIdValue);
                WbLog.Debug("[WorldBuilder] Attempting to join lobby: " + inputValue);

                SteamAPICall_t apiCall = SteamMatchmaking.JoinLobby(lobbyId);
                WbLog.Debug("[WorldBuilder] JoinLobby called - waiting for callback...");

                ConnectedUI.createDisconnectBtn(sceneManager);
            }
            else
            {
                ConsoleBase.WriteError("[WorldBuilder] Invalid lobby ID format. Must be a number.");
            }
        }
    }
}