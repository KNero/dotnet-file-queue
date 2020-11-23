using Knero.FileQueue.Converter;

namespace Knero.FileQueue
{
    public class QueueConfig
    {
        public string QueueDirectory { get; set; }
        public string QueueName { get; set; }
        public IDataConverter DataConverter { get; set; }
        public long MaxQueueSize { get; set; } = 3221225472;
        public int DequeueTimeoutMilliseconds { get; set; } = -1;
        public int ReadBufferSize { get; set; } = -1;
    }
}
