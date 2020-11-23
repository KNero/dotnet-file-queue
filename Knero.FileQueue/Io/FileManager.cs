using System;
using System.IO;
using System.Text;

namespace Knero.FileQueue.Io
{
    internal class FileManager : IDisposable
    {
        private readonly static byte[] FILE_END_STAMP = Encoding.ASCII.GetBytes("_file_end_stamp_");

        private readonly long maxFileSize;

        private readonly DirectoryInfo queueDirectory;
        private readonly DirectoryInfo errorDirectory;
        private readonly FileMetaHolder metaHolder;

        private FileStream readStream;
        private FileStream writeStream;
        private readonly int readSize;

        private readonly bool isUseReadBuffer;
        private readonly byte[] readBuffer;
        private int readBufferReadOffset;
        private int readBufferWriteOffset;

        internal FileManager(string queueParentDirectory, string queueName, long maxFileSize, int readSize, int readBufferSize)
        {
            DirectoryInfo parentDirectory = new DirectoryInfo(queueParentDirectory);
            if (!parentDirectory.Exists)
            {
                throw new DirectoryNotFoundException(queueParentDirectory);
            }

            this.maxFileSize = maxFileSize;
            this.readSize = readSize;

            isUseReadBuffer = readBufferSize > 0;
            if (isUseReadBuffer)
            {
                readBuffer = new byte[readBufferSize];
                readBufferReadOffset = 0;
                readBufferWriteOffset = 0;
            }

            string queueDirectoryName = Path.Combine(parentDirectory.FullName, queueName);
            string errorDirectoryName = Path.Combine(queueDirectoryName, "error");
            
            errorDirectory = new DirectoryInfo(errorDirectoryName);
            if (!errorDirectory.Exists)
            {
                errorDirectory.Create();
            }

            queueDirectory = new DirectoryInfo(queueDirectoryName);
            if (!queueDirectory.Exists)
            {
                queueDirectory.Create();
            }

            metaHolder = new FileMetaHolder(Path.Combine(queueDirectory.FullName, queueName + ".meta"));
            readStream = new FileStream(GetFileFullName(metaHolder.ReadFileNameIndex), FileMode.OpenOrCreate, FileAccess.Read, FileShare.Write);
            writeStream = new FileStream(GetFileFullName(metaHolder.WriteFileNameIndex), FileMode.Append, FileAccess.Write, FileShare.Read);

            readStream.Seek(metaHolder.ReadOffset, SeekOrigin.Begin);
        }

        private string GetFileFullName(long fileNameIndex) => Path.Combine(queueDirectory.FullName, fileNameIndex.ToString("D20") + ".queue");

        internal byte[] ReadQueueData()
        {
            if (isUseReadBuffer)
            {
                return ReadQueueDataByBuffer();
            }

            if (metaHolder.ReadFileNameIndex == metaHolder.WriteFileNameIndex && readStream.Length - metaHolder.ReadOffset < readSize)
            {
                return null;
            }

            byte[] buf = new byte[readSize];
            int readCount = readStream.Read(buf, 0, readSize);

            if (readCount < readSize)
            {
                if (readCount == FILE_END_STAMP.Length && Util.CompareBytes(buf, 0, FILE_END_STAMP))
                {
                    MoveNextQueueFile(true);
                    return ReadQueueData();
                }
                else
                {
                    readStream.Seek(-readCount, SeekOrigin.Current);
                    return null;
                }
            }

            lock (metaHolder)
            {
                metaHolder.MoveReadOffset(readStream.Position);
            }

            byte[] result = new byte[readCount];
            Buffer.BlockCopy(buf, 0, result, 0, readCount);

            return result;
        }

        private byte[] ReadQueueDataByBuffer()
        {
            if (readBufferReadOffset == readBuffer.Length)
            {
                readBufferReadOffset = 0;

                if (readBufferWriteOffset == readBuffer.Length)
                {
                    readBufferWriteOffset = 0;
                }
            }

            
            if (readBufferReadOffset + readSize <= readBufferWriteOffset)
            {
                byte[] temp = new byte[readSize];
                Buffer.BlockCopy(readBuffer, readBufferReadOffset, temp, 0, readSize);
                readBufferReadOffset += readSize;

                lock (metaHolder)
                {
                    metaHolder.MoveReadOffset(metaHolder.ReadOffset + readSize);
                }

                return temp;
            }
            else if (readBufferReadOffset + FILE_END_STAMP.Length == readBufferWriteOffset && Util.CompareBytes(readBuffer, readBufferReadOffset, FILE_END_STAMP))
            {
                readBufferReadOffset = 0;
                readBufferWriteOffset = 0;
                MoveNextQueueFile(true);
                return ReadQueueDataByBuffer();
            }
            else if (readBufferWriteOffset < readBuffer.Length)
            {
                byte[] buf = new byte[readBuffer.Length - readBufferWriteOffset];
                int readCount = readStream.Read(buf, 0, buf.Length);
                if (readCount > 0)
                {
                    Buffer.BlockCopy(buf, 0, readBuffer, readBufferWriteOffset, readCount);
                    readBufferWriteOffset += readCount;
                    return ReadQueueDataByBuffer();
                }
                else
                {
                    return null;
                }
            }

            return null;
        }

        internal void WriteQueueData(byte[] data)
        {
            writeStream.Write(data, 0, data.Length);
            writeStream.Flush();

            if (writeStream.Length >= maxFileSize)
            {
                MoveNextQueueFile(false);
            }
        }

        private void MoveNextQueueFile(bool isRead)
        {
            lock (metaHolder)
            {
                if (isRead)
                {
                    readStream.Dispose();
                    File.Delete(GetFileFullName(metaHolder.ReadFileNameIndex));

                    metaHolder.IncreaseReadFileNameIndex();
                    readStream = new FileStream(GetFileFullName(metaHolder.ReadFileNameIndex), FileMode.OpenOrCreate, FileAccess.Read, FileShare.Write);
                }
                else
                {
                    writeStream.Write(FILE_END_STAMP, 0, FILE_END_STAMP.Length);
                    writeStream.Dispose();

                    metaHolder.IncreaseWriteFileNameIndex();
                    writeStream = new FileStream(GetFileFullName(metaHolder.WriteFileNameIndex), FileMode.Append, FileAccess.Write, FileShare.Read);
                }
            }
        }

        public void WriteErrorQueueData(byte[] data, bool isRead)
        {
            string fileName = (isRead ? "Fail_Dequeue_" : "Fail_Enqueue_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "-" + Guid.NewGuid().ToString();
            string fileFullName = Path.Combine(errorDirectory.FullName, fileName);

            using (FileStream errorStream = new FileStream(fileFullName, FileMode.CreateNew))
            {
                errorStream.Write(data, 0, data.Length);
            }
        }

        public void Dispose()
        {
            if (readStream != null)
            {
                readStream.Dispose();
            }

            if (writeStream != null)
            {
                writeStream.Dispose();
            }

            if (metaHolder != null)
            {
                metaHolder.Dispose();
            }
        }
    }
}
