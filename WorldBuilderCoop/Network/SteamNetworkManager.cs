using ModLoader;
using Steamworks;
using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Network;

public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance;

    public CSteamID CurrentLobby { get; private set; }
    public CSteamID HostID { get; private set; }

    private bool _isHostOverride = false;
    public bool IsHost => _isHostOverride;

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<P2PSessionRequest_t> p2pRequest;
    Callback<LobbyChatUpdate_t> lobbyChatUpdate;

    public bool IsConnected => CurrentLobby.IsValid();

    // LOCAL MODE
    private NetworkMode _networkMode = NetworkMode.Local;
    private int _localPlayerId = -1;
    private Queue<byte[]> _receivedPackets = new Queue<byte[]>();

    private static long _globalInstanceCounter = 0;

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

    void Update()
    {
        if (_networkMode == NetworkMode.Steam)
        {
            ReceivePackets();
        }
        else
        {
            ProcessReceivedPackets();
        }
    }

    // ─────────────────────────────────────────────
    // MODE MANAGEMENT
    // ─────────────────────────────────────────────

    public bool IsLocalMode()
    {
        return _networkMode == NetworkMode.Local;
    }

    public void SetNetworkMode(NetworkMode mode)
    {
        _networkMode = mode;
        if (mode == NetworkMode.Local)
        {
            // Assign unique player ID for this instance
            _localPlayerId = (int)(++_globalInstanceCounter);
            ConsoleBase.WriteLine("[SteamNetwork] Switching to LOCAL mode");
            ConsoleBase.WriteLine("[SteamNetwork] Assigned Player ID: " + _localPlayerId);
            InitializeLocalMode();
        }
        else
        {
            ConsoleBase.WriteLine("[SteamNetwork] Switching to STEAM mode");
            InitializeSteamCallbacks();
        }
    }

    // ─────────────────────────────────────────────
    // LOCAL MODE
    // ─────────────────────────────────────────────

    private void InitializeLocalMode()
    {
        _isHostOverride = (_localPlayerId == 1);
        CurrentLobby = new CSteamID(999); // Dummy lobby ID for local mode

        ConsoleBase.WriteLine("[SteamNetwork] Local Mode Initialized");
        ConsoleBase.WriteLine("[SteamNetwork] Local Player ID: " + _localPlayerId);
        ConsoleBase.WriteLine("[SteamNetwork] I am: " + (_isHostOverride ? "HOST" : "CLIENT"));
    }

    private void ProcessReceivedPackets()
    {
        while (_receivedPackets.Count > 0)
        {
            byte[] packet = _receivedPackets.Dequeue();
            ApplyPacket(packet);
        }
    }

    // ─────────────────────────────────────────────
    // STEAM MODE
    // ─────────────────────────────────────────────

    private void InitializeSteamCallbacks()
    {
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        p2pRequest = Callback<P2PSessionRequest_t>.Create(OnP2PRequest);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    // ─────────────────────────────────────────────
    // LOBBY
    // ─────────────────────────────────────────────

    public void CreateLobby(int maxPlayers = 8)
    {
        if (_networkMode == NetworkMode.Local)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Local mode - CreateLobby ignored");
            return;
        }

        ConsoleBase.WriteLine("[SteamNetwork] Creating lobby with max " + maxPlayers + " players");
        SteamMatchmaking.CreateLobby(
            ELobbyType.k_ELobbyTypeFriendsOnly,
            maxPlayers
        );
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
        ConsoleBase.WriteLine("[SteamNetwork] Host ID: " + HostID.m_SteamID);
    }

    void OnLobbyChatUpdate(LobbyChatUpdate_t data)
    {
        EChatMemberStateChange changeType = (EChatMemberStateChange)data.m_rgfChatMemberStateChange;

        if (changeType == EChatMemberStateChange.k_EChatMemberStateChangeEntered)
        {
            CSteamID newMember = new CSteamID(data.m_ulSteamIDUserChanged);
            ConsoleBase.WriteLine("[SteamNetwork] ✓ Player joined lobby: " + newMember.m_SteamID);
            ConsoleBase.WriteLine("[SteamNetwork] Total players in lobby: " + SteamMatchmaking.GetNumLobbyMembers(CurrentLobby));

            if (IsHost)
            {
                ConsoleBase.WriteLine("[SteamNetwork] Host detected - preparing to sync map with new player");
            }
        }
        else if (changeType == EChatMemberStateChange.k_EChatMemberStateChangeLeft)
        {
            CSteamID leftMember = new CSteamID(data.m_ulSteamIDUserChanged);
            ConsoleBase.WriteLine("[SteamNetwork] ⚠ Player left lobby: " + leftMember.m_SteamID);
            ConsoleBase.WriteLine("[SteamNetwork] Total players in lobby: " + SteamMatchmaking.GetNumLobbyMembers(CurrentLobby));
        }
    }

    void OnLobbyEntered(LobbyEnter_t data)
    {
        ConsoleBase.WriteLine("[SteamNetwork] OnLobbyEntered callback triggered!");
        ConsoleBase.WriteLine("[SteamNetwork] My SteamID: " + SteamUser.GetSteamID().m_SteamID);

        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamMatchmaking.GetLobbyOwner(CurrentLobby);
        _isHostOverride = false; // Joining player is always client

        bool amHost = IsHost;
        ConsoleBase.WriteLine("[SteamNetwork] ✓ Entered lobby!");
        ConsoleBase.WriteLine("[SteamNetwork] Lobby ID: " + CurrentLobby.m_SteamID);
        ConsoleBase.WriteLine("[SteamNetwork] Host ID: " + HostID.m_SteamID);
        ConsoleBase.WriteLine("[SteamNetwork] I am: " + (amHost ? "HOST" : "CLIENT"));
        ConsoleBase.WriteLine("[SteamNetwork] Total players in lobby: " + SteamMatchmaking.GetNumLobbyMembers(CurrentLobby));
    }

    public void InviteFriends()
    {
        if (_networkMode == NetworkMode.Local)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Local mode - InviteFriends ignored");
            return;
        }

        if (CurrentLobby.IsValid())
        {
            ConsoleBase.WriteLine("[SteamNetwork] Opening invite dialog");
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        }
    }

    // ─────────────────────────────────────────────
    // P2P
    // ─────────────────────────────────────────────

    void OnP2PRequest(P2PSessionRequest_t req)
    {
        ConsoleBase.WriteLine("[SteamNetwork] P2P request from: " + req.m_steamIDRemote.m_SteamID);
        SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
    }

    public void SendToAll(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            ConsoleBase.WriteError("[SteamNetwork] Attempted to send empty data");
            return;
        }

        byte packetType = data[0];

        if (_networkMode == NetworkMode.Local)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Local sending packet type " + packetType + " (" + data.Length + " bytes)");
            _receivedPackets.Enqueue(data);
        }
        else if (IsHost)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Host sending packet type " + packetType + " to all clients (" + data.Length + " bytes)");
            CSteamID hostID = SteamUser.GetSteamID();
            Broadcast(data, hostID);
        }
        else
        {
            ConsoleBase.WriteLine("[SteamNetwork] Client sending packet type " + packetType + " to host (" + data.Length + " bytes)");
            SteamNetworking.SendP2PPacket(
                HostID,
                data,
                (uint)data.Length,
                EP2PSend.k_EP2PSendReliable
            );
        }
    }

    public void SendToHost(byte[] data)
    {
        if (IsHost) return;

        SteamNetworking.SendP2PPacket(
            HostID,
            data,
            (uint)data.Length,
            EP2PSend.k_EP2PSendReliable
        );
    }

    void Broadcast(byte[] data, CSteamID sender)
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
        int sentTo = 0;

        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);

            if (member == sender || member == SteamUser.GetSteamID())
                continue;

            SteamNetworking.SendP2PPacket(
                member,
                data,
                (uint)data.Length,
                EP2PSend.k_EP2PSendReliable
            );
            sentTo++;
        }

        ConsoleBase.WriteLine("[SteamNetwork] Broadcast sent to " + sentTo + " clients");
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
                ConsoleBase.WriteLine("[SteamNetwork] Received packet from " + sender.m_SteamID + " (" + size + " bytes, type: " + buffer[0] + ")");
                HandlePacket(sender, buffer);
            }
        }
    }

    // ─────────────────────────────────────────────
    // PACKET HANDLING
    // ─────────────────────────────────────────────

    void HandlePacket(CSteamID sender, byte[] data)
    {
        if (IsHost)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Host: processing and broadcasting packet");
            ApplyPacket(data);
            Broadcast(data, sender);
        }
        else if (sender == HostID)
        {
            ConsoleBase.WriteLine("[SteamNetwork] Client: processing packet from host");
            ApplyPacket(data);
        }
        else
        {
            ConsoleBase.WriteError("[SteamNetwork] Received packet from unknown sender: " + sender.m_SteamID);
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
                    ConsoleBase.WriteLine("[SteamNetwork] Applying PlaceObject packet");
                    PacketHandler.HandlePlaceObject(data, data.Length);
                    break;

                case Packets.RemoveObjects:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying RemoveObjects packet");
                    PacketHandler.HandleRemoveObject(data, data.Length);
                    break;

                case Packets.UpdateObjects:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying UpdateObjects packet");
                    PacketHandler.HandleUpdateObject(data, data.Length);
                    break;

                case Packets.LoadMap:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying LoadMap packet");
                    PacketHandler.HandleLoadMap(data, data.Length);
                    break;

                case Packets.PlayerSync:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying PlayerSync packet");
                    PacketHandler.HandlePlayerSync(data, data.Length);
                    break;

                case Packets.RemovePlayer:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying RemovePlayer packet");
                    PacketHandler.HandleRemovePlayer(data);
                    break;

                case Packets.AddToSelection:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying AddToSelection packet");
                    PacketHandler.HandleAddToSelection(data, data.Length);
                    break;

                case Packets.RemoveFromSelection:
                    ConsoleBase.WriteLine("[SteamNetwork] Applying RemoveFromSelection packet");
                    PacketHandler.HandleRemoveFromSelection(data, data.Length);
                    break;

                default:
                    ConsoleBase.WriteError($"[SteamNetwork] Unknown packet type: {packetType}");
                    break;
            }
        }
        catch (Exception ex)
        {
            ConsoleBase.WriteError($"[SteamNetwork] Error handling packet type {packetType}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}

public enum NetworkMode
{
    Local,  // Testing mode
    Steam   // Production mode
}