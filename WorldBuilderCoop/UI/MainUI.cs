using BrokeProtocol.Managers;
using ModLoader;
using UnityEngine.UIElements;
using WorldBuilderCoop.UI;

internal class MainUI
{
    public static void createModeSelectionUI(SceneManager sceneManager)
    {
        var root = sceneManager.uiDocument.rootVisualElement;

        VisualElement container = new VisualElement();
        container.name = "ModeSelectionPanel";
        container.style.paddingLeft = container.style.paddingRight = 20;
        container.style.paddingTop = container.style.paddingBottom = 20;
        container.style.backgroundColor = new UnityEngine.Color(0.05f, 0.05f, 0.07f, 0.8f);
        container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 10;
        container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 10;
        container.style.width = 260;

        Label titleLabel = new Label { text = "SELECT MODE" };
        titleLabel.style.fontSize = 16;
        titleLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
        titleLabel.style.color = UnityEngine.Color.white;
        titleLabel.style.marginBottom = 20;

        Button localBtn = new Button { text = "LOCAL (Test)", name = "LocalModeButton" };
        Styles.ApplyButtonStyle(localBtn);
        localBtn.clicked += () => selectMode(sceneManager, NetworkMode.Local);

        Button steamBtn = new Button { text = "STEAM (Online)", name = "SteamModeButton" };
        Styles.ApplyButtonStyle(steamBtn);
        steamBtn.clicked += () => selectMode(sceneManager, NetworkMode.Steam);

        container.Add(titleLabel);
        container.Add(localBtn);
        container.Add(steamBtn);
        root.Add(container);
    }

    private static void selectMode(SceneManager sceneManager, NetworkMode mode)
    {
        var root = sceneManager.uiDocument.rootVisualElement;
        var modePanel = root.Q<VisualElement>("ModeSelectionPanel");
        if (modePanel != null)
        {
            root.Remove(modePanel);
        }

        if (SteamNetworkManager.Instance != null)
        {
            SteamNetworkManager.Instance.SetNetworkMode(mode);
            ConsoleBase.WriteLine("[WorldBuilder] Network mode set to: " + mode);
        }

        createJoinBtn(sceneManager);
        createHostBtn(sceneManager);
    }

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
        if (SteamNetworkManager.Instance != null)
        {
            SteamNetworkManager.Instance.CreateLobby(8);
            ConsoleBase.WriteLine("[WorldBuilder] Creating lobby...");
        }
        else
            ConsoleBase.WriteError("[WorldBuilder] SteamNetworkManager not initialized");
        ConnectedUI.createDisconnectBtn(sceneManager);
    }
}
