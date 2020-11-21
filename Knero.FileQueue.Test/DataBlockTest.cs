using Knero.FileQueue.Converter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text;

namespace Knero.FileQueue.Test
{
    [Serializable]
    public class TestObject
    {
        public string Str { get; set; }
        public int Num { get; set; }
        public float Flo { get; set; }
        public TestObject To { get; set; }
    }

    [TestClass]
    public class DataBlockTest
    {
        [TestMethod]
        public void TestConvertObject()
        {
            TestObject t1 = new TestObject
            {
                Str = "t1-str",
                Num = 1236,
                Flo = 1.3f,
                To = new TestObject
                {
                    Str = "t2-str",
                    Num = 2435,
                    Flo = 5.3456f
                }
            };

            IDataConverter converter = new ObjectConverter();

            DataBlock dataBlock = DataBlock.CreateByUserData(converter.Serialize(t1));
            DataBlock getDataBlock = DataBlock.CreateByQueueData(dataBlock.QueueData);

            TestObject t2 = (TestObject) converter.Deserialize(getDataBlock.UserData);

            Assert.AreEqual(t1.Str, t2.Str);
            Assert.AreEqual(t1.Num, t2.Num);
            Assert.AreEqual(t1.Flo, t2.Flo);
            Assert.AreEqual(t1.To.Str, t2.To.Str);
            Assert.AreEqual(t1.To.Num, t2.To.Num);
            Assert.AreEqual(t1.To.Flo, t2.To.Flo);
        }

        [TestMethod]
        public void TestConvertString()
        {
            string data = "test_data_go";
            IDataConverter converter = new Utf8Converter();

            DataBlock dataBlock = DataBlock.CreateByUserData(converter.Serialize(data));
            DataBlock getDatablock = DataBlock.CreateByQueueData(dataBlock.QueueData);

            Assert.AreEqual(data, converter.Deserialize(getDatablock.UserData));
        }

        [TestMethod]
        public void TestConvertLargeString()
        {
            StringBuilder dataBuilder = new StringBuilder();
            for (int i = 0; i < 512 * 2 + 23; ++i)
            {
                Random r = new Random();
                dataBuilder.Append(r.Next() % 10);
            }

            string data = dataBuilder.ToString();
            IDataConverter converter = new Utf8Converter();

            DataBlock dataBlock = DataBlock.CreateByUserData(converter.Serialize(data));
            DataBlock getDatablock = DataBlock.CreateByQueueData(dataBlock.QueueData);

            Assert.AreEqual(data, converter.Deserialize(getDatablock.UserData));
        }
    }
}
