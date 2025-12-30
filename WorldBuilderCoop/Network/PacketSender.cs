using BrokeProtocol.Client.Builder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    internal class PacketSender
    {
        public const int TargetFPS = 30;
        public const float SyncIntervalLoadMap = 1f / TargetFPS;

        public static void SendPlaceObject(Vector3 position, Quaternion rotation, Vector3 scale, int objectId, string prefabName, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.PlaceObject);
                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);
                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);
                    writer.Write(scale.x);
                    writer.Write(scale.y);
                    writer.Write(scale.z);
                    writer.Write(objectId);
                    writer.Write(prefabName);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                    Core.Network.SendPacket(packet, distribution, userIds);
                }
            }
        }

        public static void SendRemoveObject(List<int> objectIds, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.RemoveObjects);
                    writer.Write(objectIds.Count);

                    foreach (var objectId in objectIds)
                    {
                        writer.Write(objectId);
                    }

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                    Core.Network.SendPacket(packet, distribution, userIds);
                }
            }
        }

        public static void SendUpdateObject(List<int> objectIds, Vector3 position, Quaternion rotation, Vector3 scale, PacketDistribution distribution = PacketDistribution.SendToAll, byte[] componentData = null, List<int> userIds = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);

                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.UpdateObjects);

                    if (objectIds != null)
                    {
                        writer.Write(objectIds.Count);
                        foreach (int id in objectIds)
                        {
                            writer.Write(id);
                        }
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);

                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);

                    writer.Write(scale.x);
                    writer.Write(scale.y);
                    writer.Write(scale.z);

                    if (componentData != null && componentData.Length > 0)
                    {
                        writer.Write(componentData.Length);
                        writer.Write(componentData);
                    }
                    else
                    {
                        writer.Write(0);
                    }

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);

                    Core.Network.SendPacket(packet, distribution, userIds);
                }
            }
        }

        public static void SendPlayerSync(int userId, Vector3 position, Quaternion rotation, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.PlayerSync);
                    writer.Write(userId);
                    writer.Write(position.x);
                    writer.Write(position.y);
                    writer.Write(position.z);
                    writer.Write(rotation.x);
                    writer.Write(rotation.y);
                    writer.Write(rotation.z);
                    writer.Write(rotation.w);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);

                    Core.Network.SendPacket(packet, distribution, userIds);
                }
            }
        }

        public static void SendLoadMap(List<ObjectInfo> objects, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            BlEditorManager.Instance.StartCoroutine(loadMapByChuncks(objects, distribution, userIds));
        }

        private static IEnumerator loadMapByChuncks(List<ObjectInfo> objects, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            int chunkSize = 30;
            int totalObjects = objects.Count;

            for (int i = 0; i < totalObjects; i += chunkSize)
            {
                List<ObjectInfo> currentChunk = objects.GetRange(i, Math.Min(chunkSize, totalObjects - i));
                bool isFirstChunk = (i == 0);
                bool isLastChunk = (i + chunkSize >= totalObjects);

                using (var ms = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write(0);
                        writer.Write((byte)distribution);
                        writer.Write((byte)Packets.LoadMap);
                        writer.Write(currentChunk.Count);
                        writer.Write(isFirstChunk);
                        writer.Write(totalObjects);
                        writer.Write(isLastChunk);

                        foreach (var obj in currentChunk)
                        {
                            writer.Write(obj.objectId);
                            writer.Write(obj.position.x); writer.Write(obj.position.y); writer.Write(obj.position.z);
                            writer.Write(obj.rotation.x); writer.Write(obj.rotation.y); writer.Write(obj.rotation.z); writer.Write(obj.rotation.w);
                            writer.Write(obj.scale.x); writer.Write(obj.scale.y); writer.Write(obj.scale.z);
                            writer.Write(obj.prefabIndex);
                            writer.Write(obj.placeIndex);
                        }

                        byte[] packet = ms.ToArray();
                        int dataLength = packet.Length - 4;
                        Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                        Core.Network.SendPacket(packet, distribution, userIds);
                    }
                }

                if (i + chunkSize < totalObjects)
                {
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }

        public static void SendRemovePlayer(int userId, PacketDistribution distribution = PacketDistribution.SendToAll, List<int> userIds = null)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.RemovePlayer);
                    writer.Write(userId);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                    Core.Network.SendPacket(packet, distribution, userIds);
                }
            }
        }

        public static void SendAddToSelection(int userId, int objectId, PacketDistribution distribution = PacketDistribution.SendToOthers)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.AddToSelection);
                    writer.Write(userId);
                    writer.Write(objectId);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                    Core.Network.SendPacket(packet, distribution);
                }
            }
        }

        public static void SendRemoveToSelection(int userId, int objectId, PacketDistribution distribution = PacketDistribution.SendToOthers)
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(0);
                    writer.Write((byte)distribution);
                    writer.Write((byte)Packets.RemoveFromSelection);
                    writer.Write(userId);
                    writer.Write(objectId);

                    byte[] packet = ms.ToArray();
                    int dataLength = packet.Length - 4;
                    Buffer.BlockCopy(BitConverter.GetBytes(dataLength), 0, packet, 0, 4);
                    Core.Network.SendPacket(packet, distribution);
                }
            }
        }
    }
}