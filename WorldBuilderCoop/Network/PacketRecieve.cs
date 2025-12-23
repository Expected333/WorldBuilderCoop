using ModLoader;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldBuilderCoop.Network
{
    internal class PacketRecieve
    {
        public static void HandlePlaceObject(byte[] data, int length)
        {
            int offset = 2;
            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );
            offset += 16;

            Vector3 scale = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            int objectId = BitConverter.ToInt32(data, offset);
            offset += 4;

            int prefabNameLength = BitConverter.ToInt32(data, offset);
            offset += 4;

            string prefabName = System.Text.Encoding.UTF8.GetString(data, offset, prefabNameLength);

            WorldBuilderSync.placeObject(position, rotation, scale, objectId, prefabName);
        }

        public static void HandleRemoveObject(byte[] data, int length)
        {
            int offset = 2;
            int count = BitConverter.ToInt32(data, offset);
            offset += 4;

            List<int> objectIds = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int objectId = BitConverter.ToInt32(data, offset);
                objectIds.Add(objectId);
                offset += 4;
            }
            ConsoleBase.WriteLine("packet recieved " + objectIds.Count);
            WorldBuilderSync.destroyObject(objectIds);
        }

        public static void HandleUpdateObject(byte[] data, int length)
        {
            int offset = 2;
            int objectId = BitConverter.ToInt32(data, offset);
            offset += 4;

            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );
            offset += 16;

            Vector3 scale = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            int componentDataLength = BitConverter.ToInt32(data, offset);
            offset += 4;

            byte[] componentData = componentDataLength > 0 ? new byte[componentDataLength] : null;
            if (componentDataLength > 0)
            {
                Buffer.BlockCopy(data, offset, componentData, 0, componentDataLength);
            }

            WorldBuilderSync.updateObject(objectId, position, rotation, scale, componentData);
        }

        public static void HandleLoadMap(byte[] data, int length)
        {
            int nameLength = BitConverter.ToInt32(data, 2);
            string mapName = System.Text.Encoding.UTF8.GetString(data, 6, nameLength);
            WorldBuilderSync.loadMap(mapName);
        }

        public static void HandlePlayerSync(byte[] data, int length)
        {
            int offset = 2;
            int userId = BitConverter.ToInt32(data, offset);
            offset += 4;

            Vector3 position = new Vector3(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8)
            );
            offset += 12;

            Quaternion rotation = new Quaternion(
                BitConverter.ToSingle(data, offset),
                BitConverter.ToSingle(data, offset + 4),
                BitConverter.ToSingle(data, offset + 8),
                BitConverter.ToSingle(data, offset + 12)
            );

            WorldBuilderSync.userSync(userId, position, rotation);
        }
    }
}
