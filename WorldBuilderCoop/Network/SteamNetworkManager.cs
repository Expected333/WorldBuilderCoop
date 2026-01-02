using BrokeProtocol.Client.Builder;
using BrokeProtocol.Utility;
using ModLoader;
using Steamworks;
using System;
using System.Net.Sockets;
using UnityEngine;
using WorldBuilderCoop;
using WorldBuilderCoop.Network;

public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance;

    private readonly System.Collections.Generic.Queue<Action> _mainThreadQueue = new System.Collections.Generic.Queue<Action>();
    private readonly System.Collections.Generic.Queue<byte[]> _packetQueue = new System.Collections.Generic.Queue<byte[]>();
    private bool _isLoadingMap = false;

    public CSteamID CurrentLobby { get; private set; }
    public CSteamID HostID { get; private set; }
    private const int STEAM_CHANNEL = 2;

    private bool _isHostOverride = false;
    public bool IsHost => _isHostOverride;
    public bool IsConnected
    {
        get
        {
            if (_networkMode == NetworkMode.Local)
                return _localNetwork != null && _localNetwork.IsConnected;
            return CurrentLobby.IsValid();
        }
    }

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<P2PSessionRequest_t> p2pRequest;
    Callback<LobbyChatUpdate_t> lobbyChatUpdate;

    private NetworkMode _networkMode = NetworkMode.Local;
    private LocalNetworkManager _localNetwork;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        _networkMode = NetworkMode.Local;
        ConsoleBase.WriteLine("[SteamNetwork] Waiting for mode selection from UI...");
    }

    void OnDestroy()
    {
        DisconnectLocal();
    }

    void Update()
    {
        lock (_mainThreadQueue)
        {
            while (_mainThreadQueue.Count > 0)
            {
                _mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        if (_networkMode == NetworkMode.Steam)
        {
            SteamAPI.RunCallbacks();
            ReceivePackets();
        }
    }

    public void RunOnMainThread(Action action)
    {
        if (action == null) return;
        lock (_mainThreadQueue)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    public bool IsLocalMode() => _networkMode == NetworkMode.Local;

    public void SetNetworkMode(NetworkMode mode)
    {
        _networkMode = mode;

        if (mode == NetworkMode.Local)
        {
            _localNetwork = new LocalNetworkManager();
            InitializeLocalMode();
        }
        else
        {
            ConsoleBase.WriteLine("[SteamNetwork] Switching to STEAM mode");
            InitializeSteamCallbacks();
        }
    }

    private void InitializeLocalMode()
    {
        ConsoleBase.WriteLine("[SteamNetwork] Switching to LOCAL mode");
        CurrentLobby = new CSteamID(999);

        bool hostStarted = _localNetwork.TryStartHost(OnClientConnected);

        if (hostStarted)
        {
            _isHostOverride = true;
            ConsoleBase.WriteLine("[SteamNetwork] I am: HOST");
            ConsoleBase.WriteLine("[SteamNetwork] ✓ Lobby created (host)");
        }
        else
        {
            _isHostOverride = false;
            ConsoleBase.WriteLine("[SteamNetwork] I am: CLIENT");
            StartAsClient();
        }
    }

    private void StartAsClient()
    {
        _localNetwork.StartClient(
            onSuccess: () =>
            {
                ConsoleBase.WriteLine("[SteamNetwork] ✓ Connected to existing lobby");
                SendPlayerJoinNotification();
                StartCoroutine(ListenToHost());
            },
            onError: (error) =>
            {
                ConsoleBase.WriteError("[SteamNetwork] Connection error: " + error);
            }
        );
    }

    private System.Collections.IEnumerator ListenToHost()
    {
        while (_localNetwork.IsConnected && !IsHost)
        {
            try
            {
                byte[] packet = _localNetwork.ReceivePacket();
                if (packet != null && packet.Length > 0)
                {
                    ApplyPacket(packet);
                }
            }
            catch (System.Exception ex)
            {
                ConsoleBase.WriteError("[SteamNetwork] Client listen error: " + ex.Message);
                break;
            }

            yield return new WaitForSeconds(0.01f);
        }
    }

    private void OnClientConnected(TcpClient client)
    {
        ConsoleBase.WriteLine("[SteamNetwork] ✓ Player joined lobby");

        if (_networkMode == NetworkMode.Local)
        {
            RunOnMainThread(() => SendAllPlayersToLocalClient(client));
        }

        System.Threading.Tasks.Task.Run(() =>
        {
            _localNetwork.ListenToClient(client, (data) =>
            {
                RunOnMainThread(() => ApplyPacket(data));
            });
        });
    }

    private void SendPlayerJoinNotification()
    {
        var camera = MonoBehaviourSingleton<BlSceneCamera>.Instance;
        if (camera != null)
        {
            int userId = GetCurrentUserId();
            int placeIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;
            byte[] data = PlayerSyncHelper.SerializePlayerSync(userId, camera.mTransform.position, camera.mTransform.rotation, placeIndex);
            SendToAll(data);
        }
    }

    private int GetCurrentUserId()
    {
        if (_networkMode == NetworkMode.Local)
        {
            return LocalUserManager.GetLocalUserId();
        }
        return SteamUser.GetSteamID().m_SteamID.GetHashCode();
    }

    private void InitializeSteamCallbacks()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        p2pRequest = Callback<P2PSessionRequest_t>.Create(OnP2PRequest);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    public void CreateLobby(int maxPlayers = 8)
    {
        if (_networkMode == NetworkMode.Local) return;

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
    }

    void OnLobbyCreated(LobbyCreated_t data)
    {
        if (data.m_eResult != EResult.k_EResultOK)
        {
            ConsoleBase.WriteError("[SteamNetwork] Lobby creation error: " + data.m_eResult);
            return;
        }

        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamUser.GetSteamID();
        _isHostOverride = true;

        ConsoleBase.WriteLine("[SteamNetwork] ✓ Lobby created successfully!");
        ConsoleBase.WriteLine("[SteamNetwork] Lobby ID: " + CurrentLobby.m_SteamID);
    }

    void OnLobbyChatUpdate(LobbyChatUpdate_t data)
    {
        EChatMemberStateChange changeType = (EChatMemberStateChange)data.m_rgfChatMemberStateChange;

        if (changeType == EChatMemberStateChange.k_EChatMemberStateChangeEntered)
        {
            CSteamID newPlayerId = new CSteamID(data.m_ulSteamIDUserChanged);
            ConsoleBase.WriteLine("[SteamNetwork] ✓ Player joined: " + newPlayerId.m_SteamID);

            if (newPlayerId != SteamUser.GetSteamID())
            {
                RunOnMainThread(() => SendMyInfoToSteamUser(newPlayerId));
            }

            if (IsHost)
                ConsoleBase.WriteLine("[SteamNetwork] Syncing map with new player");
        }
    }

    private void SendMyInfoToSteamUser(CSteamID target)
    {
        var camera = MonoBehaviourSingleton<BlSceneCamera>.Instance;
        if (camera != null)
        {
            int userId = GetCurrentUserId();
            int placeIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;
            byte[] data = PlayerSyncHelper.SerializePlayerSync(userId, camera.mTransform.position, camera.mTransform.rotation, placeIndex);
            ConsoleBase.WriteLine($"[SteamNetwork] Sending my info ({userId}) to new Steam player {target.m_SteamID}");
            SteamNetworking.SendP2PPacket(target, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, STEAM_CHANNEL);
        }
    }

    private void SendAllPlayersToLocalClient(TcpClient client)
    {
        try
        {
            // 1. Send Host (Self)
            var camera = MonoBehaviourSingleton<BlSceneCamera>.Instance;
            if (camera != null)
            {
                int userId = GetCurrentUserId();
                int placeIndex = BrokeProtocol.Managers.SceneManager.Instance != null ? BrokeProtocol.Managers.SceneManager.Instance.currentPlace : 0;
                byte[] data = PlayerSyncHelper.SerializePlayerSync(userId, camera.mTransform.position, camera.mTransform.rotation, placeIndex);
                ConsoleBase.WriteLine($"[SteamNetwork] Sending Host info ({userId}) to new Local client");
                _localNetwork.SendPacket(data, client.GetStream());
            }

            // 2. Send other known avatars (Existing Clients)
            UserAvatar[] avatars = FindObjectsByType<UserAvatar>(FindObjectsSortMode.None);
            foreach (var avatar in avatars)
            {
                if (avatar.UserId != GetCurrentUserId())
                {
                    byte[] data = PlayerSyncHelper.SerializePlayerSync(avatar.UserId, avatar.position, avatar.rotation, avatar.placeIndex);
                    ConsoleBase.WriteLine($"[SteamNetwork] Sending existing player ({avatar.UserId}) to new Local client");
                    _localNetwork.SendPacket(data, client.GetStream());
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleBase.WriteError($"[SteamNetwork] Error syncing players to new client: {ex.Message}");
        }
    }

    void OnLobbyEntered(LobbyEnter_t data)
    {
        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamMatchmaking.GetLobbyOwner(CurrentLobby);
        _isHostOverride = false;

        ConsoleBase.WriteLine("[SteamNetwork] ✓ Entered lobby!");
        ConsoleBase.WriteLine("[SteamNetwork] Host ID: " + HostID.m_SteamID);

        // Force accept P2P session with Host
        ConsoleBase.WriteLine($"[SteamNetwork] Force Accepting P2P with Host: {HostID.m_SteamID}");
        SteamNetworking.AcceptP2PSessionWithUser(HostID);

        SendPlayerJoinNotification();
    }

    public void InviteFriends()
    {
        if (_networkMode != NetworkMode.Local && CurrentLobby.IsValid())
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
    }

    void OnP2PRequest(P2PSessionRequest_t req)
    {
        ConsoleBase.WriteLine($"[SteamNetwork] Accepted P2P Request from {req.m_steamIDRemote}");
        SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
    }

    public void SendToAll(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            ConsoleBase.WriteError("[SteamNetwork] Empty data");
            return;
        }

        ConsoleBase.WriteLine($"[SteamNetwork] SendToAll called - Mode: {_networkMode}, PacketType: {data[0]}");

        if (_networkMode == NetworkMode.Local)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Sending via LOCAL network");
            _localNetwork.SendToAll(data, IsHost);
        }
        else if (IsHost)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Sending via STEAM (Host broadcast)");
            SendSteamBroadcast(data);
        }
        else
        {
            ConsoleBase.WriteLine("[SteamNetwork] Sending via STEAM (Client to Host)");
            SendSteamToHost(data);
        }
    }

    public void SendToHost(byte[] data)
    {
        if (IsHost) return;

        if (_networkMode == NetworkMode.Local)
            _localNetwork.SendToHost(data);
        else
            SendSteamToHost(data);
    }

    private void SendSteamBroadcast(byte[] data, CSteamID excludeId = default(CSteamID))
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
            if (member != SteamUser.GetSteamID() && member != excludeId)
            {
                SteamNetworking.SendP2PPacket(member, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, STEAM_CHANNEL);
            }
        }
    }

    private void SendSteamToHost(byte[] data)
    {
        SteamNetworking.SendP2PPacket(HostID, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable, STEAM_CHANNEL);
    }

    void ReceivePackets()
    {
        uint size;
        while (SteamNetworking.IsP2PPacketAvailable(out size, STEAM_CHANNEL))
        {
            byte[] buffer = new byte[size];
            CSteamID sender;

            if (SteamNetworking.ReadP2PPacket(buffer, size, out size, out sender, STEAM_CHANNEL))
            {
                ConsoleBase.WriteLine($"[SteamNetwork] RX Packet {buffer[0]} from {sender}");
                HandlePacket(sender, buffer);
            }
        }
    }

    void HandlePacket(CSteamID sender, byte[] data)
    {
        if (IsHost)
        {
            ApplyPacket(data);
            SendSteamBroadcast(data, sender);
        }
        else if (sender == HostID)
        {
            ApplyPacket(data);
        }
    }

    void ApplyPacket(byte[] data)
    {
        if (data == null || data.Length < 1) return;

        byte packetType = data[0];

        // Auto-detect start of map load
        if ((Packets)packetType == Packets.LoadMap)
        {
            if (!_isLoadingMap)
            {
                ConsoleBase.WriteLine("[SteamNetwork] Starting map download - Queueing subsequent packets");
                _isLoadingMap = true;
            }
        }

        // Queue packets if we are loading the map (except map data itself)
        if (_isLoadingMap && (Packets)packetType != Packets.LoadMap && (Packets)packetType != Packets.LoadMapFinished)
        {
            _packetQueue.Enqueue(data);
            return;
        }

        try
        {
            switch ((Packets)packetType)
            {
                case Packets.PlaceObject:
                    PacketHandler.HandlePlaceObject(data);
                    break;
                case Packets.RemoveObjects:
                    PacketHandler.HandleRemoveObjects(data);
                    break;
                case Packets.UpdateObjects:
                    PacketHandler.HandleUpdateObject(data, data.Length);//
                    break;
                case Packets.LoadMap:
                    PacketHandler.HandleLoadMap(data, data.Length);
                    break;
                case Packets.PlayerSync:
                    PacketHandler.HandlePlayerSync(data);
                    break;
                case Packets.RemovePlayer:
                    PacketHandler.HandleRemovePlayer(data);
                    break;
                case Packets.AddToSelection:
                    PacketHandler.HandleAddToSelection(data, data.Length);
                    break;
                case Packets.RemoveFromSelection:
                    PacketHandler.HandleRemoveFromSelection(data, data.Length);
                    break;
                case Packets.LoadMapFinished:
                    PacketHandler.HandleLoadMapFinished(data);
                    break;
                case Packets.Undo:
                    PacketHandler.HandleUndo(data);
                    break;
                case Packets.Redo:
                    PacketHandler.HandleRedo(data);
                    break;
                case Packets.Duplicate:
                    PacketHandler.HandleDuplicate(data);
                    break;
                case Packets.SaveHistory:
                    PacketHandler.HandleSaveHistory(data);
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleBase.WriteError($"[SteamNetwork] Packet error ({packetType}): {ex.Message}");
        }
    }

    public void OnMapLoadCompleted()
    {
        if (_isLoadingMap)
        {
            _isLoadingMap = false;
            ConsoleBase.WriteLine($"[SteamNetwork] Map load complete. Processing {_packetQueue.Count} queued packets.");

            // Process all queued packets
            while (_packetQueue.Count > 0)
            {
                ApplyPacket(_packetQueue.Dequeue());
            }
        }
    }

    public void DisconnectLocal()
    {
        if (_localNetwork != null)
        {
            _localNetwork.Disconnect();
        }
    }
}

public enum NetworkMode
{
    Local,
    Steam
}