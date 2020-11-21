using Knero.FileQueue.Converter;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Knero.FileQueue.Test
{
    [TestClass]
    public class FileQueueTest
    {
        [TestMethod]
        public void SimpleTest()
        {
            QueueConfig config = new QueueConfig()
            {
                QueueDirectory = @"d:\workspace\data\test-queue",
                DataConverter = new ObjectConverter(),
                QueueName = "test01"
            };

            TestObject t1 = new TestObject()
            {
                Str = "t1-str",
                Num = 123,
                Flo = 1.3234f,
                To = new TestObject()
                {
                    Str = "t2-str",
                    Num = 35654,
                    Flo = 4935.2394f,
                }
            };

            IFileQueue<TestObject> fq = FileQueue<TestObject>.Create(config);
            fq.Enqueue(t1);

            TestObject t2 = fq.Dequeue();

            Assert.AreEqual(t1.Str, t2.Str);
            Assert.AreEqual(t1.Num, t2.Num);
            Assert.AreEqual(t1.Flo, t2.Flo);
            Assert.AreEqual(t1.To.Str, t2.To.Str);
            Assert.AreEqual(t1.To.Num, t2.To.Num);
            Assert.AreEqual(t1.To.Flo, t2.To.Flo);
        }

        [TestMethod]
        public void MultiThreadTest()
        {
            QueueConfig config = new QueueConfig()
            {
                QueueDirectory = @"d:\workspace\data\test-queue",
                DataConverter = new Utf8Converter(),
                QueueName = "test02"
            };

            IFileQueue<string> fq = FileQueue<string>.Create(config);
            HashSet<string> writeData = new HashSet<string>();
            int successCount = 0;
            int failCount = 0;
            Thread[] ta = new Thread[400];

            for (int i = 0; i < ta.Length; ++i)
            {
                if (i % 2 == 0)
                {
                    ta[i] = new Thread(() =>
                    {
                        StringBuilder data = new StringBuilder();
                        for (int j = 0; j < 20; ++j)
                        {
                            data.Append(Guid.NewGuid().ToString());
                        }

                        lock (writeData)
                        {
                            writeData.Add(data.ToString());
                        }

                        fq.Enqueue(data.ToString());
                    });
                }
                else if (i % 2 == 1)
                {
                    ta[i] = new Thread(() =>
                    {
                        while (true)
                        {
                            string data = fq.Dequeue();
                            if (data != null)
                            {
                                lock (writeData)
                                {
                                    if (writeData.Contains(data))
                                    {
                                        writeData.Remove(data);
                                        Interlocked.Increment(ref successCount);
                                    }
                                    else
                                    {
                                        Interlocked.Decrement(ref failCount);
                                    }
                                }

                                break;
                            }
                        }
                    });
                }
            }

            for (int i = 0; i < ta.Length; ++i)
            {
                ta[i].Start();
            }

            while (true)
            {
                if (successCount + failCount == ta.Length / 2)
                {
                    break;
                }

                Thread.Sleep(1000);
            }

            Assert.AreEqual(successCount, ta.Length / 2);
        }

        [TestMethod]
        public void MoveFileTest()
        {
            QueueConfig config = new QueueConfig()
            {
                QueueDirectory = @"d:\workspace\data\test-queue",
                DataConverter = new Utf8Converter(),
                QueueName = "test03",
                MaxQueueSize = 1024 * 100
            };

            IFileQueue<string> fq = FileQueue<string>.Create(config);
            HashSet<string> writeData = new HashSet<string>();
            int successCount = 0;
            int failCount = 0;
            int dataCount = 2000;
            
            Thread t1 = new Thread(() =>
            {
                for (int i = 0; i < dataCount; ++i)
                {
                    StringBuilder data = new StringBuilder();
                    for (int j = 0; j < 20; ++j)
                    {
                        data.Append(Guid.NewGuid().ToString());
                    }

                    lock (writeData)
                    {
                        writeData.Add(data.ToString());
                    }

                    fq.Enqueue(data.ToString());
                }
            });

            Thread t2 =new Thread(() =>
            {
                for (int i = 0; i < dataCount; ++i)
                {
                    while (true)
                    {
                        string data = fq.Dequeue();
                        if (data != null)
                        {
                            lock (writeData)
                            {
                                if (writeData.Contains(data))
                                {
                                    writeData.Remove(data);
                                    Interlocked.Increment(ref successCount);
                                }
                                else
                                {
                                    Interlocked.Decrement(ref failCount);
                                }
                            }

                            break;
                        }
                    }
                }
            });

            t1.Start();
            t2.Start();

            while (true)
            {
                if (successCount + failCount == dataCount)
                {
                    break;
                }

                Thread.Sleep(1000);
            }

            Assert.AreEqual(successCount, dataCount);
        }

        [TestMethod]
        public void DequeueTimeoutTest()
        {
            int timeout = 5000;
            QueueConfig config = new QueueConfig()
            {
                QueueDirectory = @"d:\workspace\data\test-queue",
                DataConverter = new ObjectConverter(),
                QueueName = "test04",
                DequeueTimeoutMilliseconds = timeout
            };

            IFileQueue<string> fq = FileQueue<string>.Create(config);
            DateTime s = DateTime.Now;
            try
            {
                fq.Dequeue();
                Assert.Fail();
            }
            catch (DequeueTimeoutException e)
            { 
                if (DateTime.Now - s < TimeSpan.FromMilliseconds(timeout))
                {
                    Assert.Fail();
                }
                else
                {
                    Assert.IsFalse(e.IsBroken);
                }
            }
        }

        public void DequeueTimeoutExceptionBrokenTest()
        {
            QueueConfig config = new QueueConfig()
            {
                QueueDirectory = @"d:\workspace\data\test-queue",
                DataConverter = new Utf8Converter(),
                QueueName = "test05",
                DequeueTimeoutMilliseconds = 5000
            };

            IFileQueue<string> fq = FileQueue<string>.Create(config);

            StringBuilder data = new StringBuilder();
            for (int j = 0; j < 1000; ++j)
            {
                data.Append(Guid.NewGuid().ToString());
            }

            fq.Enqueue(data.ToString());

            try
            {
                string result = fq.Dequeue();
            }
            catch (DequeueTimeoutException e)
            {
                byte[] remain = fq.DequeueRawData();

                byte[] whole = new byte[e.QueueData.Length + remain.Length];
                using (MemoryStream wholeStream = new MemoryStream(whole))
                {
                    wholeStream.Write(e.QueueData, 0, e.QueueData.Length);
                    wholeStream.Write(remain, 0, remain.Length);
                }

                string result = fq.DeserializeQueueData(whole);

                Assert.AreEqual(data.ToString(), result);
            }
        }
    }
}
