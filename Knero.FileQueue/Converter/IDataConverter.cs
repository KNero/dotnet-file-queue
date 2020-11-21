namespace Knero.FileQueue.Converter
{
    public interface IDataConverter
    {
        byte[] Serialize(object o);

        object Deserialize(byte[] data);
    }
}
