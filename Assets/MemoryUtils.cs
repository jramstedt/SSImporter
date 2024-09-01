using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.Serialization;

namespace SS {
    [GenerateTestsForBurstCompatibility]
    public unsafe struct ListBinaryWriter : BinaryWriter {
        private NativeList<byte> content;

        /// <summary>
        /// A pointer to the data that has been written to memory.
        /// </summary>
        public readonly byte* Data => content.GetUnsafePtr();

        /// <summary>
        /// The total length of the all written data.
        /// </summary>
        public readonly int Length => content.Length;

        /// <summary>
        /// Gets or sets the current write position of the MemoryBinaryWriter.
        /// </summary>
        public long Position { get; set; }

        public ListBinaryWriter(NativeList<byte> content) {
            this.content = content;
            Position = 0;
        }
        
        /// <summary>
        /// Disposes the MemoryBinaryWriter.
        /// </summary>
        public void Dispose()
        {
        }

        internal NativeArray<byte> GetContentAsNativeArray() => content.AsArray();

        /// <summary>
        /// Writes the specified number of bytes and advances the current write position by that number of bytes.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <param name="bytes">The number of bytes to write.</param>
        public void WriteBytes(void* data, int bytes)
        {
            content.ResizeUninitialized((int)Position + bytes);
            UnsafeUtility.MemCpy(content.GetUnsafePtr() + (int)Position, data, bytes);
            Position += bytes;
        }
        
        public void WriteBytes(byte value, int bytes) {
            content.ResizeUninitialized((int)Position + bytes);
            UnsafeUtility.MemSet(content.GetUnsafePtr() + (int)Position, value, bytes);
            Position += bytes;
        }
        
        public void CopyBytes(ref BufferBinaryReader reader, int bytes) {
            content.ResizeUninitialized((int)Position + bytes);
            reader.ReadBytes(content.GetUnsafePtr() + Position, bytes);
            Position += bytes;
        }
    }
    
    [GenerateTestsForBurstCompatibility]
    public unsafe struct BufferBinaryWriter : BinaryWriter
    {
        readonly byte* content;
        readonly long length;

        /// <summary>
        /// Gets or sets the current write position of the BufferBinaryWriter.
        /// </summary>
        public long Position { get; set; }

        public readonly long Length => length;
        public readonly byte* Content => content;

        public BufferBinaryWriter(byte* content, long length) {
            this.content = content;
            this.length = length;
            Position = 0L;
        }
        
        /// <summary>
        /// Disposes the BufferBinaryWriter.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Writes the specified number of bytes and advances the current write position by that number of bytes.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <param name="bytes">The number of bytes to write.</param>
        public void WriteBytes(void* data, int bytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("WriteBytes writes beyond end of memory block");
#endif
            
            UnsafeUtility.MemCpy(content + Position, data, bytes);
            Position += bytes;
        }

        public void WriteBytes(byte value, int bytes) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("SetBytes writes beyond end of memory block");
#endif
            
            UnsafeUtility.MemSet(content + Position, value, bytes);
            Position += bytes;
        }
        
        public void CopyBytes(ref BufferBinaryReader reader, int bytes) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("CopyBytes writes beyond end of memory block");
#endif
            reader.ReadBytes(content + Position, bytes);
            Position += bytes;
        }
    }
    
    [GenerateTestsForBurstCompatibility]
    public unsafe struct BufferBinaryReader : BinaryReader
    {
        readonly byte* content;
        readonly long length;

        /// <summary>
        /// Gets or sets the current read position of the MemoryBinaryReader.
        /// </summary>
        public long Position { get; set; }
        
        public readonly long Length => length;
        public readonly byte* Content => content;

        /// <summary>
        /// Initializes and returns an instance of MemoryBinaryReader.
        /// </summary>
        /// <param name="content">A pointer to the data to read.</param>
        /// <param name="length">The length of the data to read.</param>
        public BufferBinaryReader(byte* content, long length)
        {
            this.content = content;
            this.length = length;
            Position = 0L;
        }

        /// <summary>
        /// Disposes the MemoryBinaryReader.
        /// </summary>
        public void Dispose()
        {
        }
        
        /// <summary>
        /// Reads a byte and advances the current read position by a byte.
        /// </summary>
        /// <returns>The read data.</returns>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public byte ReadByte()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + sizeof(byte) > length)
                throw new ArgumentException("ReadByte reads beyond end of memory block");
#endif
            var res = *(content + Position);
            Position += sizeof(byte);
            return res;
        }

        /// <summary>
        /// Reads the specified number of bytes and advances the current read position by that number of bytes.
        /// </summary>
        /// <param name="data">The read data.</param>
        /// <param name="bytes">The number of bytes to read.</param>
        /// <exception cref="ArgumentException">Thrown if attempting read beyond the end of the memory block.</exception>
        public void ReadBytes(void* data, int bytes)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
            if (Position + bytes > length)
                throw new ArgumentException("ReadBytes reads beyond end of memory block");
#endif
            UnsafeUtility.MemCpy(data, content + Position, bytes);
            Position += bytes;
        }
            
        public unsafe T Read<T>() where T : unmanaged {
            T value;
            ReadBytes(&value, sizeof(T));
            return value;
        }
    }
}