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

        internal FileManager(string queueParentDirectory, string queueName, long maxFileSize)
        {
            DirectoryInfo parentDirectory = new DirectoryInfo(queueParentDirectory);
            if (!parentDirectory.Exists)
            {
                throw new DirectoryNotFoundException(queueParentDirectory);
            }

            this.maxFileSize = maxFileSize;
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

        internal byte[] ReadQueueData(int readSize)
        {
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
                    return ReadQueueData(readSize);
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
