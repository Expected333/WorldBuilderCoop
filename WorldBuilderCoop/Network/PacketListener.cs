using ModLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    public class PacketListener
    {
        private readonly TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private byte[] _sizeBuffer = new byte[4];
        private bool _isConnected;
        private Action<byte[], int> _onPacketReceived;

        public PacketListener(TcpClient tcpClient, bool isConnected, Action<byte[], int> onPacketReceived)
        {
            _tcpClient = tcpClient;
            _isConnected = isConnected;
            _onPacketReceived = onPacketReceived;
        }

        public IEnumerator ListenPacketLoop()
        {
            yield return new WaitForSeconds(0.5f);

            while (_isConnected)
            {
                if (_tcpClient?.Connected == true && EnsureStream() && _tcpClient.Available >= NetworkConfig.HeaderSize)
                {
                    if (TryReadInt32(_networkStream) is int size && size > 0 && size <= NetworkConfig.MaxPacketSize)
                    {
                        if (WaitForData(size) && TryReadBuffer(_networkStream, size) is byte[] packet)
                            _onPacketReceived?.Invoke(packet, packet.Length);
                    }
                }
                yield return new WaitForSeconds(NetworkConfig.SyncInterval);
            }
        }

        private bool EnsureStream()
        {
            try
            {
                if (_networkStream == null)
                    _networkStream = _tcpClient.GetStream();
                return _networkStream != null;
            }
            catch { return false; }
        }

        private int? TryReadInt32(NetworkStream stream)
        {
            try { stream.Read(_sizeBuffer, 0, 4); return BitConverter.ToInt32(_sizeBuffer, 0); }
            catch (Exception ex) { ConsoleBase.WriteError($"Read error: {ex.Message}"); return null; }
        }

        private byte[] TryReadBuffer(NetworkStream stream, int size)
        {
            byte[] buffer = new byte[size];
            try
            {
                int read = 0;
                while (read < size && (read += stream.Read(buffer, read, size - read)) > 0) ;
                return read == size ? buffer : null;
            }
            catch (Exception ex) { ConsoleBase.WriteError($"Packet error: {ex.Message}"); return null; }
        }

        private bool WaitForData(int size)
        {
            float start = Time.time;
            while (_tcpClient.Available < size && _isConnected && Time.time - start < NetworkConfig.PacketTimeout) { }
            return _tcpClient.Available >= size;
        }

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

        public static void HandleAddToSelection(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    var distribution = reader.ReadByte();
                    var packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    int objectId = reader.ReadInt32();
                    WorldBuilderSync.AddToSelection(userId, objectId);
                }
            }
        }

        public static void HandleRemoveFromSelection(byte[] data, int length)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new BinaryReader(ms))
                {
                    var distribution = reader.ReadByte();
                    var packetType = reader.ReadByte();
                    int userId = reader.ReadInt32();
                    int objectId = reader.ReadInt32();
                    WorldBuilderSync.RemoveFromSelection(userId, objectId);
                }
            }
        }
    }
}