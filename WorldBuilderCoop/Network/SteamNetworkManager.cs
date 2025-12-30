using Steamworks;
using UnityEngine;

public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance;

    public CSteamID CurrentLobby { get; private set; }
    public CSteamID HostID { get; private set; }

    public bool IsHost => SteamUser.GetSteamID() == HostID;

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<P2PSessionRequest_t> p2pRequest;
    public bool IsConnected => CurrentLobby.IsValid();
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
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        p2pRequest = Callback<P2PSessionRequest_t>.Create(OnP2PRequest);
        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    void Update()
    {
        ReceivePackets();
    }

    // ─────────────────────────────────────────────
    // LOBBY
    // ─────────────────────────────────────────────

    public void CreateLobby(int maxPlayers = 8)
    {
        SteamMatchmaking.CreateLobby(
            ELobbyType.k_ELobbyTypeFriendsOnly,
            maxPlayers
        );
    }

    void OnLobbyCreated(LobbyCreated_t data)
    {
        if (data.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Lobby creation error");
            return;
        }

        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamUser.GetSteamID();

        Debug.Log("Lobby created. Host: " + HostID);
    }
    void OnLobbyChatUpdate(LobbyChatUpdate_t data)
    {

    }

    void OnLobbyEntered(LobbyEnter_t data)
    {
        CurrentLobby = new CSteamID(data.m_ulSteamIDLobby);
        HostID = SteamMatchmaking.GetLobbyOwner(CurrentLobby);

        Debug.Log("Joined to lobby. Host: " + HostID);
    }

    public void InviteFriends()
    {
        if (CurrentLobby.IsValid())
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
    }

    // ─────────────────────────────────────────────
    // P2P
    // ─────────────────────────────────────────────

    void OnP2PRequest(P2PSessionRequest_t req)
    {
        SteamNetworking.AcceptP2PSessionWithUser(req.m_steamIDRemote);
    }

    // Client → Host
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

    // Host → All
    void Broadcast(byte[] data, CSteamID sender)
    {
        int count = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);

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
        }
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

    // ─────────────────────────────────────────────
    // PACKET HANDLING
    // ─────────────────────────────────────────────

    void HandlePacket(CSteamID sender, byte[] data)
    {
        // If i'm host → apply and send
        if (IsHost)
        {
            ApplyPacket(data);
            Broadcast(data, sender);
        }
        // if I'm client → Just apply
        else if (sender == HostID)
        {
            ApplyPacket(data);
        }
    }

    void ApplyPacket(byte[] data)
    {
        // Call construction system
        // Example:
        // PacketParser.Parse(data);
    }
}
