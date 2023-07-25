using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static PersistenceList.Engine.AsyncEnumerableExtensions;

namespace PersistenceList
{
    public sealed class BufferCollection<T> : IDisposable
    {
        private Action<T[]> flushMethod;
        private T[] buffer;
        private readonly int bufferLength;
        private readonly bool useTask;
        private Task lockerTask = Task.Run(() => { });
        private int arrayIndex;

        public BufferCollection(int bufferLength, Action<T[]> flushMethod, bool useTask = false)
        {
            if (flushMethod == null || bufferLength < 1) throw new ArgumentNullException();
            this.arrayIndex = 0;
            this.flushMethod = flushMethod;
            this.useTask = useTask;
            this.buffer = new T[this.bufferLength = bufferLength];
        }

        public void Add(T data)
        {
            if (flushMethod == null) throw new ObjectDisposedException(this.GetType().Name);
            lock (lockerTask)
            {
                this.buffer[arrayIndex++] = data;
                if (arrayIndex == this.bufferLength) this.Flush(false);
            }
        }

        private void Flush(bool isFinal)
        {
            if (useTask) lockerTask.Wait();
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
                Task newTask = null;
                if (useTask) {
                    T[] oldBuffer = this.buffer;
                    newTask = new Task(() => this.flushMethod(oldBuffer));
                } else this.flushMethod(this.buffer);
                this.buffer = new T[this.bufferLength];
                this.arrayIndex = 0;
                if (newTask != null) (lockerTask = newTask).Start();
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

        public static async IAsyncEnumerable<T[]> PartitionAsync(IEnumerable<T> items, int partitionSize, [EnumeratorCancellation] CancellationToken cancellationToken = default(CancellationToken))
        {
            if (items == null || partitionSize <= 0) yield break;
            var elements = items.AsAsyncEnumerable().WithCancellation(cancellationToken).GetAsyncEnumerator();
            try
            {
                bool firstElement = true;
                bool canRead = true;
                while (canRead)
                {
                    var output = new T[partitionSize];
                    for (int i = 0; i < partitionSize; i++)
                    {
                        canRead = await elements.MoveNextAsync();
                        if (!canRead) { Array.Resize(ref output, i); break; }
                        firstElement = false;
                        output[i] = elements.Current;
                    }
                    if (firstElement && !canRead) yield break;
                    yield return output;
                }
            }
            finally
            {
                await elements.DisposeAsync();
            }
        }

        public void Dispose()
        {
            if (flushMethod == null) throw new ObjectDisposedException(this.GetType().Name);
            lock (lockerTask)
            {
                this.Flush(true);
                flushMethod = null;
                buffer = null;
            }
        }
    }
}
