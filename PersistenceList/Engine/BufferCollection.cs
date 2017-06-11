using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PersistenceList
{
    public sealed class BufferCollection<T> : IDisposable
    {
        private Action<T[]> flushMethod;
        private T[] buffer;
        private readonly int bufferLength;
        private int arrayIndex;

        public BufferCollection(int bufferLength, Action<T[]> flushMethod)
        {
            if (flushMethod == null || bufferLength < 1) throw new ArgumentNullException();
            this.arrayIndex = 0;
            this.flushMethod = flushMethod;
            this.buffer = new T[this.bufferLength = bufferLength];
        }

        public void Add(T data)
        {
            if (flushMethod == null) throw new ObjectDisposedException(this.GetType().Name);
            this.buffer[arrayIndex++] = data;
            if (arrayIndex == this.bufferLength) this.Flush(false);
        }

        private void Flush(bool isFinal)
        {
            if (isFinal)
            {
                if (arrayIndex != 0)
                {
                    Array.Resize(ref this.buffer, arrayIndex);
                    this.flushMethod(this.buffer);
                }
            }
            else
            {
                this.flushMethod(this.buffer);
                this.buffer = new T[this.bufferLength];
                this.arrayIndex = 0;
            }
        }

        public static IEnumerable<T[]> Partition(IEnumerable<T> items, int partitionSize)
        {
            if (items == null || partitionSize <= 0) yield break;
            using (var elements = items.GetEnumerator())
            {
                bool firstElement = true;
                bool canRead = true;
                while (canRead)
                {
                    var output = new T[partitionSize];
                    for (int i = 0; i < partitionSize; i++)
                    {
                        canRead = elements.MoveNext();
                        if (!canRead) { Array.Resize(ref output, i); break; }
                        firstElement = false;
                        output[i] = elements.Current;
                    }
                    if (firstElement && !canRead) yield break;
                    yield return output;
                }
            }
        }

        public void Dispose()
        {
            if (flushMethod == null) throw new ObjectDisposedException(this.GetType().Name);
            this.Flush(true);
            flushMethod = null;
            buffer = null;
        }
    }
}
