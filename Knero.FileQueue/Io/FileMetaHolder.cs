using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace Knero.FileQueue.Io
{
    internal class FileMetaHolder : IDisposable
    {
        private readonly static int readFileNameIndexPosition = 0;
        private readonly static int readOffsetPosition = readFileNameIndexPosition + 8;
        private readonly static int writeFileNameIndexPosition = readOffsetPosition + 8;
        private readonly static int totalFileSize = 8 * 3;

        private readonly MemoryMappedFile metaFile;

        internal long ReadFileNameIndex { get; private set; }
        internal long ReadOffset { get; private set; }

        internal long WriteFileNameIndex { get; private set; }

        internal FileMetaHolder(string filePath)
        {
            if (!File.Exists(filePath))
            {
                CreateAndInitMetaFile(filePath);
                metaFile = MemoryMappedFile.CreateFromFile(filePath);
            }
            else
            {
                try
                {
                    metaFile = MemoryMappedFile.CreateFromFile(filePath);
                    byte[] buf = new byte[totalFileSize];
                    
                    using (MemoryMappedViewAccessor accessor = metaFile.CreateViewAccessor())
                    {
                        accessor.ReadArray(readFileNameIndexPosition, buf, 0, buf.Length);
                    }

                    ReadFileNameIndex = BitConverter.ToInt64(buf, readFileNameIndexPosition);
                    ReadOffset = BitConverter.ToInt64(buf, readOffsetPosition);
                    WriteFileNameIndex = BitConverter.ToInt64(buf, writeFileNameIndexPosition);
                }
                catch (Exception e)
                {
                    Dispose();
                    throw e;
                }
            }
        }

        private void CreateAndInitMetaFile(string filePath)
        {
            using (FileStream metaFile = File.Create(filePath)) 
            {
                byte[] zero = BitConverter.GetBytes(0L);
                metaFile.Write(zero, 0, zero.Length); // ReadFileNameIndex
                metaFile.Write(zero, 0, zero.Length); // ReadOffset
                metaFile.Write(zero, 0, zero.Length); // WriteFileNameIndex
            }
        }

        internal void IncreaseReadFileNameIndex()
        {
            UpdateRead(ReadFileNameIndex + 1, 0);
        }

        internal void MoveReadOffset(long offset)
        {
            UpdateRead(ReadFileNameIndex, offset);
        }

        internal void IncreaseWriteFileNameIndex()
        {
            UpdateWrite(WriteFileNameIndex + 1);
        }

        private void UpdateRead(long fileNameIndex, long offset)
        {
            using (MemoryMappedViewAccessor accessor = metaFile.CreateViewAccessor())
            {
                byte[] buf = new byte[16];

                byte[] nameBuf = BitConverter.GetBytes(fileNameIndex);
                Buffer.BlockCopy(nameBuf, 0, buf, 0, nameBuf.Length);

                byte[] indexBuf = BitConverter.GetBytes(offset);
                Buffer.BlockCopy(indexBuf, 0, buf, nameBuf.Length, indexBuf.Length);

                accessor.WriteArray(readFileNameIndexPosition, buf, 0, buf.Length);
            }

            ReadFileNameIndex = fileNameIndex;
            ReadOffset = offset;
        }

        private void UpdateWrite(long fileNameIndex)
        {
            using (MemoryMappedViewAccessor accessor = metaFile.CreateViewAccessor())
            {
                byte[] buf = new byte[totalFileSize / 2];

                byte[] nameBuf = BitConverter.GetBytes(fileNameIndex);
                Buffer.BlockCopy(nameBuf, 0, buf, 0, nameBuf.Length);

                accessor.WriteArray(writeFileNameIndexPosition, buf, 0, buf.Length);
            }

            WriteFileNameIndex = fileNameIndex;
        }

        public void Dispose()
        {
            if (metaFile != null)
            {
                metaFile.Dispose();
            }
        }
    }
}
