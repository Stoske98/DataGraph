#if DATAGRAPH_ENTITIES
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace DataGraph.Data
{
    /// <summary>
    /// Utility for saving and loading BlobAssetReference to/from files.
    /// Uses MemoryBinaryWriter/Reader with unsafe byte access since
    /// StreamBinaryWriter/Reader are internal to Unity.Entities.
    /// </summary>
    public static class BlobFileUtility
    {
        /// <summary>
        /// Saves a BlobAssetReference to a file at the given path.
        /// </summary>
        public static unsafe void Save<T>(BlobAssetReference<T> blobRef, string path) where T : unmanaged
        {
            using var writer = new MemoryBinaryWriter();
            writer.Write(blobRef);

            var bytes = new byte[writer.Length];
            fixed (byte* dest = bytes)
            {
                UnsafeUtility.MemCpy(dest, writer.Data, writer.Length);
            }
            System.IO.File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Loads a BlobAssetReference from a file at the given path.
        /// </summary>
        public static unsafe BlobAssetReference<T> Load<T>(string path) where T : unmanaged
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            fixed (byte* ptr = bytes)
            {
                using var reader = new MemoryBinaryReader(ptr, bytes.Length);
                return reader.Read<T>();
            }
        }
    }
}
#endif
