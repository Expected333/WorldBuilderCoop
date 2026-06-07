using BrokeProtocol.Managers;
using ModLoader;
using Steamworks;
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

            bool isLocalMode = SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsLocalMode();
            if (!isLocalMode)
            {
                Button copyIdBtn = new Button { text = "COPY MY LOBBY ID", name = "CopyIdButton" };
                Styles.ApplyButtonStyle(copyIdBtn);
                copyIdBtn.clicked += () => copyCurrentLobbyId();
                root.Add(copyIdBtn);
            }
        }

        public static void destroyDisconnectBtn(SceneManager sceneManager)
        {
            var root = sceneManager.uiDocument.rootVisualElement;
            var disconnectBtn = root.Q<Button>("Disconnect");
            var copyIdBtn = root.Q<Button>("CopyIdButton");

            if (disconnectBtn != null)
                root.Remove(disconnectBtn);
            if (copyIdBtn != null)
                root.Remove(copyIdBtn);
        }

        public static void disconnect(SceneManager sceneManager)
        {
            // Properly close network connections
            if (SteamNetworkManager.Instance != null)
            {
                bool isLocalMode = SteamNetworkManager.Instance.IsLocalMode();

                if (isLocalMode)
                {
                    SteamNetworkManager.Instance.DisconnectLocal();
                    WbLog.Debug("[WorldBuilder] Disconnected from local lobby");
                }
                else if (SteamNetworkManager.Instance.IsConnected)
                {
                    SteamMatchmaking.LeaveLobby(SteamNetworkManager.Instance.CurrentLobby);
                    WbLog.Debug("[WorldBuilder] Disconnected from Steam lobby");
                }
            }

            destroyDisconnectBtn(sceneManager);
            MainUI.createModeSelectionUI(sceneManager);
        }

        private static void copyCurrentLobbyId()
        {
            if (SteamNetworkManager.Instance != null && SteamNetworkManager.Instance.IsConnected)
            {
                string lobbyId = SteamNetworkManager.Instance.CurrentLobby.m_SteamID.ToString();
                GUIUtility.systemCopyBuffer = lobbyId;
                WbLog.Debug("[WorldBuilder] Lobby ID copied to clipboard: " + lobbyId);
            }
            else
            {
                ConsoleBase.WriteError("[WorldBuilder] Not connected to a lobby");
            }
        }
    }
}