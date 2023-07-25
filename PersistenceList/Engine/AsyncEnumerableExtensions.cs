using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PersistenceList.Engine
{
    //https://stackoverflow.com/questions/55384089/how-can-i-adapt-a-taskienumerablet-to-iasyncenumerablet
    public static class AsyncEnumerableExtensions
    {
        public struct AsyncEnumerable<T> : IAsyncEnumerable<T>
        {
            private readonly IEnumerable<T> enumerable;

            public AsyncEnumerable(IEnumerable<T> enumerable)
            {
                this.enumerable = enumerable;
            }

            public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default(CancellationToken))
            {
                return new AsyncEnumerator<T>(enumerable?.GetEnumerator(), cancellationToken);
            }
        }

        public struct AsyncEnumerator<T> : IAsyncEnumerator<T>
        {
            private readonly IEnumerator<T> enumerator;
            private readonly CancellationToken cancellationToken;

            public AsyncEnumerator(IEnumerator<T> enumerator, CancellationToken cancellationToken = default(CancellationToken))
            {
                this.enumerator = enumerator;
                this.cancellationToken = cancellationToken;
            }

            public ValueTask DisposeAsync()
            {
                enumerator?.Dispose();
                return default;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                if (cancellationToken.IsCancellationRequested) await Task.FromCanceled(cancellationToken);
                return enumerator == null ? false : enumerator.MoveNext();
            }

            public T Current => enumerator.Current;
        }

        public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this IEnumerable<T> that)
        {
            if (that == null) return null;
            var asyncEnumerable = that as IAsyncEnumerable<T>;
            if (asyncEnumerable != null) return asyncEnumerable;
            return new AsyncEnumerable<T>(that);
        }

        public static IAsyncEnumerator<T> AsAsyncEnumerator<T>(this IEnumerator<T> that, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (that == null) return null;
            var asyncEnumerator = that as IAsyncEnumerator<T>;
            if (asyncEnumerator != null) return asyncEnumerator;
            return new AsyncEnumerator<T>(that, cancellationToken);
        }
    }
}
