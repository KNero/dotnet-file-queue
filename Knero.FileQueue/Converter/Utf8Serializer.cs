using System.Text;

namespace Knero.FileQueue.Converter
{
    public class Utf8Serializer : IDataConverter
    {
        public object Deserialize(byte[] data)
        {
            return Encoding.UTF8.GetString(data);
        }

        public byte[] Serialize(object o)
        {
            return Encoding.UTF8.GetBytes((string) o);
        }
    }
}
