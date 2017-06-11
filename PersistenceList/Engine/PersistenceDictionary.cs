using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PersistenceList
{
    [Serializable][DebuggerDisplay("Count = {Count}"), System.Runtime.InteropServices.ComVisible(false)]
    [DebuggerTypeProxy(typeof(PersistenceDictionary_DictionaryDebugView<,>))]

    public sealed class PersistenceDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable
    {
        private PersistenceList<TValue> persistencer = null;
        private Dictionary<TKey, int> persistenceIndexes = new Dictionary<TKey, int>();

        public PersistenceDictionary(PersistenceList<TValue> persistModule = null) 
        {
            persistencer = persistModule ?? new PersistenceList<TValue>();
            if (persistencer.Count > 0) throw new InvalidOperationException("The persistencer must be Empty!");
        }

        #region IDictionary Implementation
        public TValue this[TKey key]
        {
            get
            {
                return persistencer[persistenceIndexes[key]];
            }
            set
            {
                int index = this.GetIndexByKey(key);
                if (index < 0) this.Add(key, value); //insert
                else persistencer[index] = value; //update                    
            }
        }

        public int Count
        {
            get
            {
                return persistenceIndexes.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return persistenceIndexes.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                return persistencer.AsReadOnly();
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this.Add(item.Key, item.Value);
        }

        public void Add(TKey key, TValue value)
        {
            lock (persistenceIndexes)
            {
                //checks performance problem if (persistenceIndexes.ContainsValue(newPosition)) throw new ...
                persistenceIndexes.Add(key, persistenceIndexes.Count);
                try { persistencer.Add(value);}
                catch(Exception ex)
                {
                    persistenceIndexes.Remove(key); //rollback
                    throw ex;
                }
            }
        }

        public void Clear()
        {
            lock (persistenceIndexes)
            {
                persistenceIndexes.Clear();
                persistencer.Clear();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (!persistenceIndexes.ContainsKey(item.Key)) return false;
            var value = persistencer[persistenceIndexes[item.Key]];
            return object.Equals(value, item.Value);
        }

        public bool ContainsKey(TKey key)
        {
            return persistenceIndexes.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            if (arrayIndex < 0 || arrayIndex > array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }
            if (array.Length - arrayIndex < this.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(array));
            }
            using (var numerator = this.GetEnumerator())
            {
                while (numerator.MoveNext())
                {
                    array[arrayIndex++] = numerator.Current;
                }
            }           
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.MakeInternalEnumerator();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return this.Contains(item) ? this.Remove(item.Key) : false;
        }

        public bool Remove(TKey key)
        {
            int oldIndexValue;
            if (persistenceIndexes.TryGetValue(key, out oldIndexValue))
            {
                lock (persistenceIndexes)
                {
                    persistencer.RemoveAt(oldIndexValue);
                    persistenceIndexes.Remove(key);
                    this.ReIndex();
                }
                return true;
            }
            return false;
        }
            
        public bool TryGetValue(TKey key, out TValue value)
        {
            int index;
            if (!persistenceIndexes.TryGetValue(key, out index))
            {
                value = default(TValue);
                return false;
            }
            value = persistencer[index];
            return true;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.MakeInternalEnumerator();
        }

        #endregion

        #region Bulk Operations

        public BufferCollection<KeyValuePair<TKey, TValue>> MakeBufferedAdd(bool bySize = true)
        {
            return new BufferCollection<KeyValuePair<TKey, TValue>>(PersistenceList<TValue>.MAX_SQLITE_COMMAND_PARAMETERS, data => 
            {
                lock (persistenceIndexes)
                {
                    List<TKey> added = new List<TKey>();
                    try
                    {
                        foreach (var e in data) { persistenceIndexes.Add(e.Key, persistenceIndexes.Count); added.Add(e.Key); };
                        persistencer.AddRange(data.Select(o => o.Value), bySize);
                    }
                    catch (Exception e)
                    {
                        added.ForEach(k => persistenceIndexes.Remove(k)); //rollback
                        throw e;
                    }
                }
            });
        }

        public void AddRange<TSource>(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, Func<TSource, TValue> elementSelector, bool bySize = true)
        {
            if (source == null || keySelector == null || elementSelector == null) throw new ArgumentNullException();
            using (var buffer = this.MakeBufferedAdd(bySize))
                foreach (var e in source) buffer.Add(new KeyValuePair<TKey, TValue>(keySelector(e), elementSelector(e)));
        }

        public void RemoveAt(int index)
        {
            lock (persistenceIndexes)
            {
                var key = GetKeyByIndex(index);
                persistencer.RemoveAt(index);
                persistenceIndexes.Remove(key);
                this.ReIndex();
            }
        }

        public TKey GetKeyByIndex(int index) //slow
        {
            if (index < 0 || index >= persistenceIndexes.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var key = persistenceIndexes.Where(e => e.Value == index).Select(e => e.Key);
            return key.Single(); //if cause exception, has a syncronization error
        }

        public int GetIndexByKey(TKey key)
        {
            int index;
            if (!persistenceIndexes.TryGetValue(key, out index)) return -1;
            return index;
        }

        public TValue GetValueByIndex(int index)
        {
            if (index < 0 || index >= persistenceIndexes.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return persistencer[index];
        }

        public KeyValuePair<TKey, TValue> GetItemByIndex(int index)
        {
            return new KeyValuePair<TKey, TValue>(GetKeyByIndex(index), persistencer[index]);
        }

        #endregion

        #region Materializer

        private IEnumerator<KeyValuePair<TKey, TValue>> MakeInternalEnumerator()
        {
            return this.IndexedItems().Select(e => new KeyValuePair<TKey, TValue>(e.Item2, e.Item3)).GetEnumerator();
        }

        public IEnumerable<Tuple<int, TKey,TValue>> IndexedItems()
        {
            return from persisted in persistencer.Select((e, i) => new { i, e })
                   join index in persistenceIndexes on persisted.i equals index.Value
                   select Tuple.Create(index.Value, index.Key, persisted.e);
        }

        private void ReIndex()
        {
            lock (persistenceIndexes)
            {
                var lengs = persistenceIndexes.OrderBy(e => e.Value).Select((e, i) => new { i, e.Key }).ToDictionary(e => e.Key, e => e.i);
                persistenceIndexes.Clear(); //clean
                persistenceIndexes = lengs; //replace by new reference
            }
        }

        #endregion

        public void Dispose()
        {
            persistencer?.Dispose();
            persistencer = null;
            persistenceIndexes?.Clear();
            persistenceIndexes = null;
        }

    }

    #region Debug View
    internal sealed class PersistenceDictionary_DictionaryDebugView<K, V>
    {
        private IDictionary<K, V> dict;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<K, V>[] Items
        {
            get
            {
                KeyValuePair<K, V>[] array = new KeyValuePair<K, V>[this.dict.Count];
                this.dict.CopyTo(array, 0);
                return array;
            }
        }

        public PersistenceDictionary_DictionaryDebugView(IDictionary<K, V> dictionary)
        {
            if (dictionary == null)
            {
                throw new Exception("ExceptionArgument.dictionary");
            }
            this.dict = dictionary;
        }
    }
    #endregion
}
