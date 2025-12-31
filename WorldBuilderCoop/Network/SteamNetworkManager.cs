using ModLoader;
using Steamworks;
using System;
using System.Net.Sockets;
using UnityEngine;
using WorldBuilderCoop.Network;

public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance;

    public CSteamID CurrentLobby { get; private set; }
    public CSteamID HostID { get; private set; }

    private bool _isHostOverride = false;
    public bool IsHost => _isHostOverride;
    public bool IsConnected => CurrentLobby.IsValid();

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
        if (_networkMode == NetworkMode.Steam)
            ReceivePackets();
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
        _localNetwork.ListenToClient(client, (data) =>
        {
            ApplyPacket(data);
        });
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
            ConsoleBase.WriteLine("[SteamNetwork] ✓ Player joined: " + new CSteamID(data.m_ulSteamIDUserChanged).m_SteamID);
            if (IsHost)
                ConsoleBase.WriteLine("[SteamNetwork] Syncing map with new player");
        }
    }

    void OnLobbyEntered(LobbyEnter_t data)
    {
        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamMatchmaking.GetLobbyOwner(CurrentLobby);
        _isHostOverride = false;

        ConsoleBase.WriteLine("[SteamNetwork] ✓ Entered lobby!");
        ConsoleBase.WriteLine("[SteamNetwork] Host ID: " + HostID.m_SteamID);
    }

    public void InviteFriends()
    {
        if (_networkMode != NetworkMode.Local && CurrentLobby.IsValid())
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
    }

    void OnP2PRequest(P2PSessionRequest_t req)
    {
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

    private void SendSteamBroadcast(byte[] data)
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
            if (member != SteamUser.GetSteamID())
            {
                SteamNetworking.SendP2PPacket(member, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
            }
        }
    }

    private void SendSteamToHost(byte[] data)
    {
        SteamNetworking.SendP2PPacket(HostID, data, (uint)data.Length, EP2PSend.k_EP2PSendReliable);
    }

    void ReceivePackets()
    {
        uint size;
        while (SteamNetworking.IsP2PPacketAvailable(out size))
        {
            byte[] buffer = new byte[size];
            CSteamID sender;

            if (SteamNetworking.ReadP2PPacket(buffer, size, out size, out sender))
            {
                HandlePacket(sender, buffer);
            }
        }
    }

    void HandlePacket(CSteamID sender, byte[] data)
    {
        if (IsHost)
        {
            ApplyPacket(data);
            SendSteamBroadcast(data);
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

        try
        {
            switch ((Packets)packetType)
            {
                case Packets.PlaceObject:
                    PacketHandler.HandlePlaceObject(data, data.Length);
                    break;
                case Packets.RemoveObjects:
                    PacketHandler.HandleRemoveObject(data, data.Length);
                    break;
                case Packets.UpdateObjects:
                    PacketHandler.HandleUpdateObject(data, data.Length);
                    break;
                case Packets.LoadMap:
                    PacketHandler.HandleLoadMap(data, data.Length);
                    break;
                case Packets.PlayerSync:
                    PacketHandler.HandlePlayerSync(data, data.Length);
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
            }
        }
        catch (Exception ex)
        {
            ConsoleBase.WriteError($"[SteamNetwork] Packet error ({packetType}): {ex.Message}");
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