using Knero.FileQueue.Converter;
using Knero.FileQueue.Io;
using System;
using System.IO;

namespace Knero.FileQueue
{
    public class FileQueue<T> : IFileQueue<T>
    {
        private readonly IDataConverter dataConverter;

        private readonly FileManager fileManager;

        private readonly object enqueueLock = new object();
        private readonly object dequeueLock = new object();

        private readonly bool isUseTimeout;
        private readonly TimeSpan dequeueTimeout;

        private FileQueue(QueueConfig config)
        {
            dataConverter = config.DataConverter;
            fileManager = new FileManager(config.QueueDirectory, config.QueueName, config.MaxQueueSize);

            isUseTimeout = config.DequeueTimeoutMilliseconds > 0;
            if (isUseTimeout)
            {
                dequeueTimeout = TimeSpan.FromMilliseconds(config.DequeueTimeoutMilliseconds);
            }
        }

        public static IFileQueue<T> Create(QueueConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.QueueDirectory))
            {
                throw new ArgumentNullException("QueueDirectory is null or empty.");
            } 
            else if (string.IsNullOrWhiteSpace(config.QueueName))
            {
                throw new ArgumentNullException("QueueName is null or empty.");
            }
            else if (config.DataConverter == null)
            {
                throw new ArgumentNullException("DataConverter is null");
            }

            return new FileQueue<T>(config);
        }

        public T Dequeue() => DeserializeQueueData(DequeueRawData());

        public void Enqueue(T t)
        {
            byte[] data = dataConverter.Serialize(t);

            try
            {
                DataBlock dataBlock = DataBlock.CreateByUserData(data);

                lock (enqueueLock)
                {
                    fileManager.WriteQueueData(dataBlock.QueueData);
                }
            }
            catch (Exception e)
            {
                fileManager.WriteErrorQueueData(data, false);
                throw e;
            }
        }

        public byte[] DequeueRawData()
        {
            bool isFindFooter = false;

            lock (dequeueLock)
            {
                DateTime start = DateTime.Now;

                using (MemoryStream bufStream = new MemoryStream())
                {
                    while (!isFindFooter)
                    {
                        if (isUseTimeout && DateTime.Now - start > dequeueTimeout)
                        {
                            throw new DequeueTimeoutException(bufStream.ToArray());
                        }

                        byte[] buf = fileManager.ReadQueueData(DataBlock.BlockPartSize);
                        if (buf != null && buf.Length > 0)
                        {
                            bufStream.Write(buf, 0, buf.Length);

                            isFindFooter = DataBlock.IsFooterBlockPart(buf);
                        }
                    }

                    return bufStream.ToArray();
                }
            }
        }

        public T DeserializeQueueData(byte[] queueData)
        {
            try
            {
                DataBlock dataBlock = DataBlock.CreateByQueueData(queueData);
                return (T)dataConverter.Deserialize(dataBlock.UserData);
            }
            catch (DataBlock.DataBlockParseException e)
            {
                fileManager.WriteErrorQueueData(e.QueueData, true);
                throw e;
            }
        }
    }

    public class DequeueTimeoutException : Exception
    {
        public byte[] QueueData { get; }

        internal DequeueTimeoutException(byte[] data) : base("dequeue timeout")
        {
            QueueData = data;
        }

        public bool IsBroken 
        { 
            get 
            {
                return QueueData.Length > 0;
            } 
        }
    }
}
