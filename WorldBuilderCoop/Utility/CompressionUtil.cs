using System.IO;
using System.IO.Compression;

namespace WorldBuilderCoop.Utility
{
    public static class CompressionUtil
    {
        public static byte[] Compress(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return memoryStream.ToArray();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            using (var memoryStream = new MemoryStream(data))
            {
                using (var outputStream = new MemoryStream())
                {
                    using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress))
                    {
                        gzipStream.CopyTo(outputStream);
                    }
                    return outputStream.ToArray();
                }
            }
        }
    }
}
