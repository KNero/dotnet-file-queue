namespace Knero.FileQueue
{
    public interface IFileQueue<T>
    {
        void Enqueue(T t);

        T Dequeue();

        byte[] DequeueRawData();

        T DeserializeQueueData(byte[] data);
    }
}
