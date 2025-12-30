using BrokeProtocol.Client.Builder;
using ModLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    public class NetworkHandler : MonoBehaviour
    {
        public NetworkServer _server;
        public NetworkClient _client;
        public PacketListener _packetListener;
        public List<UserAvatar> connectedClientAvatar = new List<UserAvatar>();

        private bool _isHost;
        private bool _isConnected;
        private int _myUserId = -1;

        public bool IsHost => _isHost;
        public bool IsConnected => _isConnected;
        public int MyUserId => _myUserId;

        public void CreateHost(int port = NetworkConfig.DefaultPort)
        {
            try
            {
                _server = new NetworkServer(ProcessPacket);
                _server.Start(port);
                _isHost = true;
                _isConnected = true;

                BlEditorManager.Instance.StartCoroutine(_server.ListenLoop());
                BlEditorManager.Instance.StartCoroutine(WorldBuilderSync.listenPlayerMovementLoop());
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Failed to create host: " + ex.Message);
            }
        }

        public void JoinAsClient(string ipAddress, int port = NetworkConfig.DefaultPort)
        {
            try
            {
                if (_isConnected)
                    Shutdown();

                _client = new NetworkClient();
                _client.Connect(ipAddress, port,
                    onSuccess: OnConnectedToHost,
                    onError: msg => ConsoleBase.WriteError("Connection failed: " + msg)
                );
                ConsoleBase.WriteLine("Connecting to " + ipAddress + ":" + port);
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Failed to join: " + ex.Message);
            }
        }

        private void OnConnectedToHost()
        {
            _isConnected = true;
            _packetListener = new PacketListener(_client._tcpClient, true, ProcessPacket);

            ConsoleBase.WriteLine("Connected to host. Waiting for assignment.");
            BlEditorManager.Instance.StartCoroutine(_packetListener.ListenPacketLoop());
            BlEditorManager.Instance.StartCoroutine(WorldBuilderSync.listenPlayerMovementLoop());
        }

        public void SendPacket(byte[] packet, PacketDistribution distribution, List<int> userIds = null)
        {
            try
            {
                if (_isHost)
                {
                    SendPacketAsHost(packet, distribution, userIds);
                }
                else if (_isConnected && _client != null)
                {
                    _client.SendPacket(packet);
                }
            }
            catch (Exception ex)
            {
                ConsoleBase.WriteError("Send error: " + ex.Message);
            }
        }

        private void SendPacketAsHost(byte[] packet, PacketDistribution distribution, List<int> userIds)
        {
            foreach (var client in _server.ConnectedClients)
            {
                bool shouldSend = distribution == PacketDistribution.SendToAll ||
                                 (distribution == PacketDistribution.SendToOthers && (userIds == null || !userIds.Contains(client.UserId))) ||
                                 (distribution == PacketDistribution.SendToUser && userIds != null && userIds.Contains(client.UserId));

                if (shouldSend && client.IsConnected)
                {
                    try
                    {
                        client.Stream.Write(packet, 0, packet.Length);
                        client.Stream.Flush();
                    }
                    catch { }
                }
            }
        }

        private void ProcessPacket(byte[] data, int length)
        {
            if (length < 2) return;

            PacketDistribution distribution = (PacketDistribution)data[0];
            Packets packetType = (Packets)data[1];

            switch (packetType)
            {
                case Packets.AssignID: PacketListener.HandleAssignID(data); break;
                case Packets.PlaceObject: PacketListener.HandlePlaceObject(data, length); break;
                case Packets.RemoveObjects: PacketListener.HandleRemoveObject(data, length); break;
                case Packets.UpdateObjects: PacketListener.HandleUpdateObject(data, length); break;
                case Packets.LoadMap: PacketListener.HandleLoadMap(data, length); break;
                case Packets.PlayerSync: PacketListener.HandlePlayerSync(data, length); break;
                case Packets.AddToSelection: PacketListener.HandleAddToSelection(data, length); break;
                case Packets.RemoveFromSelection: PacketListener.HandleRemoveFromSelection(data, length); break;
                case Packets.RemovePlayer: PacketListener.HandleRemovePlayer(data); break;
            }
        }

        public void SetMyUserId(int id)
        {
            _myUserId = id;
            ConsoleBase.WriteLine("Local UserID set to: " + _myUserId);
        }

        public void Disconnect()
        {
            Shutdown();
        }

        public void Shutdown()
        {
            _isConnected = false;
            if (_server != null)
                _server.Stop();
            if (_client != null)
                _client.Disconnect();
            ConsoleBase.WriteLine("Network shutdown");
        }
    }
}