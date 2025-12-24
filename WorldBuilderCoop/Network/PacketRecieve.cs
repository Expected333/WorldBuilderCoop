using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    internal class PacketRecieve
    {
        public static void HandleAssignID(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte();
                    reader.ReadByte();
                    int myNewId = reader.ReadInt32();

                    Core.Network.SetMyUserId(myNewId);
                }
            }
        }

        public static void HandlePlaceObject(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte distribution = reader.ReadByte();
                    byte packetType = reader.ReadByte();

                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    int objectId = reader.ReadInt32();
                    string prefabName = reader.ReadString();

                    WorldBuilderSync.placeObject(position, rotation, scale, objectId, prefabName);
                }
            }
        }

        public static void HandleRemoveObject(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte distribution = reader.ReadByte();
                    byte packetType = reader.ReadByte();
                    int count = reader.ReadInt32();

                    List<int> objectIds = new List<int>();
                    for (int i = 0; i < count; i++)
                    {
                        objectIds.Add(reader.ReadInt32());
                    }

                    WorldBuilderSync.destroyObject(objectIds);
                }
            }
        }

        public static void HandleUpdateObject(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte();
                    reader.ReadByte();

                    int objectIdsCount = reader.ReadInt32();
                    List<int> objectIds = new List<int>();
                    for (int i = 0; i < objectIdsCount; i++)
                    {
                        objectIds.Add(reader.ReadInt32());
                    }

                    float px = reader.ReadSingle();
                    float py = reader.ReadSingle();
                    float pz = reader.ReadSingle();
                    Vector3 position = new Vector3(px, py, pz);

                    float rx = reader.ReadSingle();
                    float ry = reader.ReadSingle();
                    float rz = reader.ReadSingle();
                    float rw = reader.ReadSingle();
                    Quaternion rotation = new Quaternion(rx, ry, rz, rw);

                    float sx = reader.ReadSingle();
                    float sy = reader.ReadSingle();
                    float sz = reader.ReadSingle();
                    Vector3 scale = new Vector3(sx, sy, sz);

                    int componentDataLength = reader.ReadInt32();
                    byte[] componentData;

                    if (componentDataLength > 0)
                    {
                        componentData = reader.ReadBytes(componentDataLength);
                    }
                    else
                    {
                        componentData = new byte[0];
                    }

                    if (objectIds.Count > 0)
                    {
                        WorldBuilderSync.updateObject(objectIds, position, rotation, scale, componentData);
                    }
                }
            }
        }

        public static void HandleLoadMap(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    reader.ReadByte();
                    reader.ReadByte();

                    int count = reader.ReadInt32();
                    bool isFirstChunk = reader.ReadBoolean();
                    int totalObjectsCount = reader.ReadInt32();
                    bool isLastChunk = reader.ReadBoolean();

                    List<ObjectInfo> objects = new List<ObjectInfo>();

                    for (int i = 0; i < count; i++)
                    {
                        objects.Add(new ObjectInfo
                        {
                            objectId = reader.ReadInt32(),
                            position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                            rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                            scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                            prefabIndex = reader.ReadInt32(),
                            placeIndex = reader.ReadInt32()
                        });
                    }

                    WorldBuilderSync.loadMap(objects, isFirstChunk, totalObjectsCount, isLastChunk);
                }
            }
        }

        public static void HandlePlayerSync(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte distribution = reader.ReadByte();
                    byte packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    Vector3 position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Quaternion rotation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    WorldBuilderSync.userSync(userId, position, rotation);
                }
            }
        }

        public static void HandleRemovePlayer(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    byte distribution = reader.ReadByte();
                    byte packetType = reader.ReadByte();
                    int userIdToRemove = reader.ReadInt32();

                    WorldBuilderSync.removeUser(userIdToRemove);
                }
            }
        }
    }
}