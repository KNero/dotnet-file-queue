using System;
using System.Collections.Generic;
using System.Text;

namespace Knero.FileQueue
{
    public interface IFileQueue<T>
    {
        void Enqueue(T t);

        T Dequeue();
    }
}
