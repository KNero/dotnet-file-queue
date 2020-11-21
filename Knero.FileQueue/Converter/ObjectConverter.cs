using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Knero.FileQueue.Converter
{
    public class ObjectConverter : IDataConverter
    {
        private readonly BinaryFormatter binaryFormatter = new BinaryFormatter();

        public object Deserialize(byte[] data)
        {
            using (Stream bufStream = new MemoryStream(data))
            {
                return binaryFormatter.Deserialize(bufStream);
            }
        }

        public byte[] Serialize(object o)
        {
            using (MemoryStream bufStream = new MemoryStream())
            {
                binaryFormatter.Serialize(bufStream, o);
                return bufStream.ToArray();
            }
        }
    }
}
