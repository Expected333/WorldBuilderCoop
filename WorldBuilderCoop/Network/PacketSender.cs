using ModLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    internal class PacketSender
    {
        public static void SendPlaceObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabName, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] prefabNameBytes = System.Text.Encoding.UTF8.GetBytes(prefabName);
            byte[] packet = new byte[2 + 12 + 16 + 12 + 4 + 4 + prefabNameBytes.Length];

            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.PlaceObject;

            int offset = 2;
            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, offset + 12, 4);
            offset += 16;

            Buffer.BlockCopy(BitConverter.GetBytes(scale.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(prefabNameBytes.Length), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(prefabNameBytes, 0, packet, offset, prefabNameBytes.Length);

            Core.Network.SendPacket(packet, distribution, userIds);
        }

        public static void SendRemoveObject(List<int> objectIds, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] packet = new byte[2 + 4 + (objectIds.Count * 4)];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.RemoveObject;

            int offset = 2;
            Buffer.BlockCopy(BitConverter.GetBytes(objectIds.Count), 0, packet, offset, 4);
            offset += 4;

            foreach (var objectId in objectIds)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
                offset += 4;
            }
            ConsoleBase.WriteLine("Packet send " + objectIds.Count);
            Core.Network.SendPacket(packet, distribution, userIds);
        }

        public static void SendUpdateObject(int objectId, Vector3 position, Quaternion rotation, Vector3 scale, PacketDistribution distribution = PacketDistribution.SendToAll, byte[] componentData = null, List<int> userIds = null)
        {
            int componentDataLength = componentData != null ? componentData.Length : 0;
            byte[] packet = new byte[2 + 4 + 12 + 16 + 12 + 4 + componentDataLength];

            int offset = 0;
            packet[offset++] = (byte)distribution;
            packet[offset++] = (byte)Packets.UpdateObject;

            Buffer.BlockCopy(BitConverter.GetBytes(objectId), 0, packet, offset, 4);
            offset += 4;

            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, offset + 12, 4);
            offset += 16;

            Buffer.BlockCopy(BitConverter.GetBytes(scale.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(scale.z), 0, packet, offset + 8, 4);
            offset += 12;

            Buffer.BlockCopy(BitConverter.GetBytes(componentDataLength), 0, packet, offset, 4);
            offset += 4;

            if (componentDataLength > 0)
            {
                Buffer.BlockCopy(componentData, 0, packet, offset, componentDataLength);
            }

            Core.Network.SendPacket(packet, distribution, userIds);
        }

        public static void SendLoadMap(string mapName, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(mapName);
            byte[] packet = new byte[2 + 4 + nameBytes.Length];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.LoadMap;
            Buffer.BlockCopy(BitConverter.GetBytes(nameBytes.Length), 0, packet, 2, 4);
            Buffer.BlockCopy(nameBytes, 0, packet, 6, nameBytes.Length);
            Core.Network.SendPacket(packet, distribution, userIds);
        }

        public static void SendPlayerSync(int userId, Vector3 position, Quaternion rotation, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            byte[] packet = new byte[2 + 4 + 12 + 16];
            packet[0] = (byte)distribution;
            packet[1] = (byte)Packets.PlayerSync;

            int offset = 2;
            Buffer.BlockCopy(BitConverter.GetBytes(userId), 0, packet, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes(position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(position.z), 0, packet, offset + 8, 4);
            offset += 12;
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.z), 0, packet, offset + 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(rotation.w), 0, packet, offset + 12, 4);

            Core.Network.SendPacket(packet, distribution, userIds);
        }
    }
}
