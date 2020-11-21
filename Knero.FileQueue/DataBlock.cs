using System;
using System.IO;
using System.Text;

namespace Knero.FileQueue
{
    /// <summary>
    /// This contains queue data and user data.
    /// Create queue data by user data or user data by queue data.
    /// 
    /// structure : [header part][data part][data part][data part][data part]...[footer part]
    /// 
    /// header part: header + data length(1byte) + data
    /// data part: data length(1byte) + data
    /// footer part: data length(1byte) + data checksum(1) + footer
    /// </summary>
    public class DataBlock
    {
        private readonly static byte DATA = (byte)'D';
        private readonly static byte FOOTER = (byte)'F';

        public static int BlockPartSize { get; } = 512; // byte
        private readonly static int partInfoSize = 6; // type(1) + checksum(1) + length(4)
        private readonly static int partUserDataSize = BlockPartSize - partInfoSize; 

        public byte[] QueueData { get; private set; }

        public byte[] UserData { get; private set; }

        private DataBlock()
        {

        }

        public static DataBlock CreateByUserData(byte[] data)
        {
            DataBlock dataBlock = new DataBlock()
            {
                UserData = data
            };
            dataBlock.CreateQueueDate();

            return dataBlock;
        }

        public static DataBlock CreateByQueueData(byte[] source)
        {
            DataBlock dataBlock = new DataBlock()
            {
                QueueData = source
            };
            dataBlock.ParseUserData();

            return dataBlock;
        }

        private void CreateDataPart(MemoryStream bufStream, byte[] userPartData)
        {
            byte[] lengthBuf = BitConverter.GetBytes(userPartData.Length);

            bufStream.WriteByte(DATA); // type
            bufStream.WriteByte(DATA); // dummy
            bufStream.Write(lengthBuf, 0, lengthBuf.Length); // length
            bufStream.Write(userPartData, 0, userPartData.Length);
        }

        private void CreateFooterPart(MemoryStream bufStream, byte[] userPartData, byte checksum)
        {
            byte[] lengthBuf = BitConverter.GetBytes(userPartData.Length);

            bufStream.WriteByte(FOOTER); // type
            bufStream.WriteByte(checksum);
            bufStream.Write(lengthBuf, 0, lengthBuf.Length); // length
            bufStream.Write(userPartData, 0, userPartData.Length);

            byte[] padding = new byte[BlockPartSize - partInfoSize - userPartData.Length];
            if (padding.Length > 0)
            {
                bufStream.Write(padding, 0, padding.Length);
            }
        }

        private void CreateQueueDate()
        {
            byte checksum = ComputeChecksum(UserData);
            int partCount = UserData.Length / partUserDataSize;
            partCount += UserData.Length % partUserDataSize > 0 ? 1 : 0;

            using (MemoryStream userDataStream = new MemoryStream(UserData))
            using (MemoryStream resultBuf = new MemoryStream())
            {
                for (int i = 0; i < partCount; ++i)
                {
                    if (i < partCount - 1)
                    {
                        byte[] partUser = new byte[partUserDataSize];
                        userDataStream.Read(partUser, 0, partUserDataSize);

                        CreateDataPart(resultBuf, partUser);
                    }
                    else
                    {
                        int size = UserData.Length % partUserDataSize;
                        size = size > 0 ? size : partUserDataSize;

                        byte[] partUser = new byte[size];
                        userDataStream.Read(partUser, 0, size);

                        CreateFooterPart(resultBuf, partUser, checksum);
                    }
                }

                QueueData = resultBuf.ToArray();
            }
        }

        private void ParseUserData()
        {
            int partCount = QueueData.Length / BlockPartSize;

            using (MemoryStream queueDataStream = new MemoryStream(QueueData))
            using (MemoryStream resultBuf = new MemoryStream())
            {
                for (int i = 0; i < partCount; ++i)
                {
                    byte[] queuePart = new byte[BlockPartSize];
                    queueDataStream.Read(queuePart, 0, BlockPartSize);

                    if (queuePart[0] != DATA && queuePart[0] != FOOTER)
                    {
                        throw new DataBlockParseException("type is not match. type is data or footer", QueueData);
                    }

                    byte[] partUserBuf = ReadPartUserData(queuePart);
                    resultBuf.Write(partUserBuf, 0, partUserBuf.Length);

                    if (queuePart[0] == FOOTER)
                    {
                        UserData = resultBuf.ToArray();

                        if (queuePart[1] != ComputeChecksum(UserData))
                        {
                            throw new DataBlockParseException("checksum type is not match.", QueueData);
                        }
                    }
                        
                }
            }
        }

        private byte[] ReadPartUserData(byte[] partData)
        {
            int length = BitConverter.ToInt32(partData, 2);
            byte[] buf = new byte[length];

            Buffer.BlockCopy(partData, partInfoSize, buf, 0, length);

            return buf;
        }

        private static byte ComputeChecksum(byte[] data)
        {
            byte sum = 0;
            unchecked // Let overflow occur without exceptions
            {
                foreach (byte b in data)
                {
                    sum += b;
                }
            }
            return sum;
        }

        public static bool IsFooterBlockPart(byte[] buf) => buf.Length > 0 && buf[0] == FOOTER;

        public class DataBlockParseException : Exception
        {
            public byte[] QueueData { get; }

            internal DataBlockParseException(string message, byte[] source) : base(message)
            {
                QueueData = source;
            }
        }
    }
}
