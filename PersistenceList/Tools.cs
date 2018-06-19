using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace PersistenceList
{
    public static class Tools
    {
        public static dynamic ToDynamic(this object obj)
        {
            if (object.ReferenceEquals(obj, null)) return null;
            if (obj is System.Dynamic.ExpandoObject) return obj;
            else if (obj is IDictionary<string, object>)
            {
                var r = (IDictionary<string, object>)(new System.Dynamic.ExpandoObject());
                foreach (var e in (IDictionary<string, object>)obj) r.Add(e.Key, e.Value);
                return r;
            }
            return obj;
        }

        public static TimeSpan TimeProfiler(Action execute)
        {
            if (execute == null) throw new ArgumentNullException();
            var t = System.Diagnostics.Stopwatch.StartNew();
            execute(); t.Stop();
            return t.Elapsed;
        }

        public static Tuple<TimeSpan, T> TimeProfiler<T>(Func<T> execute)
        {
            if (execute == null) throw new ArgumentNullException();
            var t = System.Diagnostics.Stopwatch.StartNew();
            var value = execute(); t.Stop();
            return Tuple.Create(t.Elapsed, value);
        }

        private readonly static Random random = new Random();
        //https://www.codeproject.com/articles/16583/generate-an-image-with-a-random-number
        public static Bitmap RandomImage(ref int? number)
        {
            // Create a Bitmap image
            Bitmap imageSrc = new Bitmap(155, 85);

            // Fill it randomly with white pixels
            for (int iX = 0; iX <= imageSrc.Width - 1; iX++)
            {
                for (int iY = 0; iY <= imageSrc.Height - 1; iY++)
                {
                    if (random.Next(10) > 5)
                    {
                        imageSrc.SetPixel(iX, iY, Color.White);
                    }
                }
            }

            // Create an ImageGraphics Graphics object from bitmap Image
            using (Graphics imageGraphics = Graphics.FromImage(imageSrc))
            {
                // Generate random code. 
                if (!number.HasValue) number = random.Next(10000000);
                string hiddenCode = number.Value.ToString();

                // Draw random code within Image
                using (Font drawFont = new Font("Arial", 20, FontStyle.Italic))
                {
                    using (SolidBrush drawBrush = new SolidBrush(Color.Black))
                    {
                        float x = (float)(5.0 + (random.NextDouble() * (imageSrc.Width - 120)));
                        float y = (float)(5.0 + (random.NextDouble() * (imageSrc.Height - 30)));
                        StringFormat drawFormat = new StringFormat();
                        imageGraphics.DrawString(hiddenCode, drawFont, drawBrush, x, y, drawFormat);
                    }
                }
            }
            return imageSrc;
        }

        public static byte[] RandomImage(ref int? number, System.Drawing.Imaging.ImageFormat format)
        {
            using (var ms = new MemoryStream())
            {
                using (var imageSrc = RandomImage(ref number))
                {
                    imageSrc.Save(ms, format);
                }
                return ms.ToArray();
            }
        }

        public static byte[] ArrayCopy(byte[] inputBuffer, int bufferOffset, int? count = null)
        {
            int lenght = count ?? (inputBuffer.Length - bufferOffset);
            var buf = new byte[lenght];
            Buffer.BlockCopy(inputBuffer, bufferOffset, buf, 0, lenght);
            return buf;
        }

        #region SOAP/XML Serialization

        public static string SerializeException(this Exception ex)
        {
            return SerializeSOAP(ex) ?? string.Empty;
        }

        public static string SerializeSOAP(this object obj)
        {
            if (obj == null) return null;
            var formatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter(null,
                new StreamingContext(StreamingContextStates.Persistence));
            using (var sm = new System.IO.MemoryStream())
            {
                formatter.Serialize(sm, obj);
                sm.Position = 0;
                using (var sr = new System.IO.StreamReader(sm))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static object DeserializeSOAP(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return null;
            var formatter = new System.Runtime.Serialization.Formatters.Soap.SoapFormatter(null,
                new StreamingContext(StreamingContextStates.Persistence));
            using (var sm = new System.IO.MemoryStream())
            {
                using (var sw = new System.IO.StreamWriter(sm))
                {
                    sw.Write(obj);
                    sw.Flush();
                    sm.Position = 0;
                    return formatter.Deserialize(sm);
                }
            }
        }

        public static string SerializeXML(this object obj)
        {
            if (obj == null) return string.Empty;
            var formatter = new System.Xml.Serialization.XmlSerializer(obj.GetType());
            using (var sm = new StringWriter())
            {
                formatter.Serialize(sm, obj);
                sm.Flush();
                return sm.ToString();
            }
        }

        public static T DeserializeXML<T>(this string obj)
        {
            return (T)DeserializeXML(obj, typeof(T));
        }

        //https://stackoverflow.com/questions/325426/programmatic-equivalent-of-defaulttype
        public static object GetDefaultValue(Type t)
        {
            if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
            {
                return Activator.CreateInstance(t);
            }
            else
            {
                return null;
            }
        }

        public static object DeserializeXML(this string obj, Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (string.IsNullOrWhiteSpace(obj)) return GetDefaultValue(type);
            var formatter = new System.Xml.Serialization.XmlSerializer(type);
            using (var sm = new StringReader(obj))
            {
                return formatter.Deserialize(sm);
            }
        }

        public static string SerializeWCF<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var formatter = new System.Runtime.Serialization.DataContractSerializer(obj.GetType());
            using (var sm = new StringWriter())
            {
                using (var sx = new System.Xml.XmlTextWriter(sm))
                {
                    formatter.WriteObject(sx, obj);
                    sx.Flush();
                }
                return sm.ToString();
            }
        }

        public static T DeserializeWCF<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var formatter = new System.Runtime.Serialization.DataContractSerializer(typeof(T));
            using (var sm = new System.Xml.XmlTextReader(new StringReader(obj)))
            {
                return (T)formatter.ReadObject(sm);
            }
        }

        public static string SerializeJSON<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            return serializer.Serialize(obj);
        }

        public static T DeserializeJSON<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            return serializer.Deserialize<T>(obj);
        }

        public static dynamic DeserializeJSON(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return null;
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            serializer.RecursionLimit = serializer.MaxJsonLength = int.MaxValue;
            object values = serializer.DeserializeObject(obj);
            return ToDynamic(values);
        }

        public static string SerializePB<T>(this T obj)
        {
            if (obj == null) return string.Empty;
            var formatter = ProtoBuf.Serializer.CreateFormatter<T>();
            using (var sm = new System.IO.MemoryStream())
            {
                formatter.Serialize(sm, obj);
                sm.Position = 0;
                return Convert.ToBase64String(sm.ToArray());
            }
        }

        public static T DeserializePF<T>(this string obj)
        {
            if (string.IsNullOrWhiteSpace(obj)) return default(T);
            var formatter = ProtoBuf.Serializer.CreateFormatter<T>();
            using (var sm = new System.IO.MemoryStream(Convert.FromBase64String(obj)))
            {
                sm.Position = 0;
                return (T)formatter.Deserialize(sm);
            }
        }
        #endregion SOAP/XML Serialization

        #region GenericCast

        private static readonly MethodInfo ToCastFn = ((Func<IEnumerable, IEnumerable<int>>)Enumerable.Cast<int>).Method.GetGenericMethodDefinition();
        private static readonly Dictionary<Type, GenericCastDelegate> GenericCastCache = new Dictionary<Type, GenericCastDelegate>();
        private delegate IEnumerable GenericCastDelegate(IEnumerable collection);
        public static IEnumerable GenericCast(Type type, IEnumerable collection)
        {
            if (collection == null) return null;
            GenericCastDelegate call;
            lock (GenericCastCache)
            {
                if (!GenericCastCache.TryGetValue(type, out call)) call = null;
                if (call == null)
                {
                    var nm = ToCastFn.MakeGenericMethod(new[] { type });
                    GenericCastCache[type] = call = (GenericCastDelegate)nm.CreateDelegate(typeof(GenericCastDelegate));
                }
            }
            return call(collection);
        }

        #endregion

        #region EventHacker

        // https://stackoverflow.com/questions/3783267/how-to-get-a-delegate-object-from-an-eventinfo

        private static void ClearEventHandlers(object classInstance, string eventName)
        {
            if (classInstance == null || string.IsNullOrWhiteSpace(eventName)) throw new ArgumentNullException();
            MulticastDelegate eh = GetEventHandler(classInstance, eventName);
            if (eh != null)
            {
                var rEvent = classInstance.GetType().GetEvent(eventName, BindingFlags.Public
                                                               | BindingFlags.NonPublic
                                                               | BindingFlags.Instance);
                foreach (Delegate ev in eh.GetInvocationList())
                {
                    rEvent.RemoveEventHandler(classInstance, ev);
                }
            }
        }

        private static MulticastDelegate GetEventHandler(object classInstance, string eventName)
        {
            Type classType = classInstance.GetType();
            FieldInfo eventField = classType.GetField(eventName, BindingFlags.GetField
                                                               | BindingFlags.NonPublic
                                                               | BindingFlags.Instance);

            MulticastDelegate eventDelegate = (MulticastDelegate)eventField.GetValue(classInstance);

            // eventDelegate will be null if no listeners are attached to the event
            if (eventDelegate == null)
            {
                return null;
            }

            return eventDelegate;
        }

        public static IEnumerable<Tuple<EventInfo, MethodInfo>> GetSubscribedMethods(object obj)
        {
            Func<EventInfo, FieldInfo> ei2fi =
                ei => obj.GetType().GetField(ei.Name,
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.GetField);

            return from eventInfo in obj.GetType().GetEvents()
                   let eventFieldInfo = ei2fi(eventInfo)
                   let eventFieldValue = (System.Delegate)eventFieldInfo.GetValue(obj)
                   from subscribedDelegate in (eventFieldValue?.GetInvocationList() ??
                        Enumerable.Empty<System.Delegate>()).DefaultIfEmpty()
                   select Tuple.Create(eventInfo, subscribedDelegate?.Method);
        }

        #endregion

    }

    #region FixedCollections

    //https://stackoverflow.com/questions/5852863/fixed-size-queue-which-automatically-dequeues-old-values-upon-new-enques

    [Serializable]
    public class FixedSizedQueue<T> : System.Collections.Concurrent.ConcurrentQueue<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FixedSizedQueue(int size)
        {
            Size = size;
        }

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    T outObj;
                    base.TryDequeue(out outObj);
                }
            }
        }
    }

    [Serializable]
    public class FixedSizedStack<T> : System.Collections.Concurrent.ConcurrentStack<T>
    {
        private readonly object syncObject = new object();

        public int Size { get; private set; }

        public FixedSizedStack(int size)
        {
            Size = size;
        }

        public new void Push(T obj)
        {
            base.Push(obj);
            lock (syncObject)
            {
                while (base.Count > Size)
                {
                    T outObj;
                    base.TryPop(out outObj);
                }
            }
        }
    }

    [Serializable]
    public class CircularBuffer<T> : IEnumerable<T>
    {
        readonly int size;
        readonly object locker;

        int count;
        int head;
        int rear;
        T[] values;

        public CircularBuffer(int max)
        {
            this.size = max;
            locker = new object();
            count = 0;
            head = 0;
            rear = 0;
            values = new T[size];
        }

        static int Incr(int index, int size)
        {
            return (index + 1) % size;
        }

        private void UnsafeEnsureQueueNotEmpty()
        {
            if (count == 0)
                throw new Exception("Empty queue");
        }

        public int Size { get { return size; } }
        public object SyncRoot { get { return locker; } }

        #region Count

        public int Count { get { return UnsafeCount; } }
        public int SafeCount { get { lock (locker) { return UnsafeCount; } } }
        public int UnsafeCount { get { return count; } }

        #endregion

        #region Enqueue

        public void Enqueue(T obj)
        {
            UnsafeEnqueue(obj);
        }

        public void SafeEnqueue(T obj)
        {
            lock (locker) { UnsafeEnqueue(obj); }
        }

        public void UnsafeEnqueue(T obj)
        {
            values[rear] = obj;

            if (Count == Size)
                head = Incr(head, Size);
            rear = Incr(rear, Size);
            count = Math.Min(count + 1, Size);
        }

        #endregion

        #region Dequeue

        public T Dequeue()
        {
            return UnsafeDequeue();
        }

        public T SafeDequeue()
        {
            lock (locker) { return UnsafeDequeue(); }
        }

        public T UnsafeDequeue()
        {
            UnsafeEnsureQueueNotEmpty();

            T res = values[head];
            values[head] = default(T);
            head = Incr(head, Size);
            count--;

            return res;
        }

        #endregion

        #region Peek

        public T Peek()
        {
            return UnsafePeek();
        }

        public T SafePeek()
        {
            lock (locker) { return UnsafePeek(); }
        }

        public T UnsafePeek()
        {
            UnsafeEnsureQueueNotEmpty();

            return values[head];
        }

        #endregion

        #region GetEnumerator

        public IEnumerator<T> GetEnumerator()
        {
            return UnsafeGetEnumerator();
        }

        public IEnumerator<T> SafeGetEnumerator()
        {
            lock (locker)
            {
                List<T> res = new List<T>(count);
                var enumerator = UnsafeGetEnumerator();
                while (enumerator.MoveNext())
                    res.Add(enumerator.Current);
                return res.GetEnumerator();
            }
        }

        public IEnumerator<T> UnsafeGetEnumerator()
        {
            int index = head;
            for (int i = 0; i < count; i++)
            {
                yield return values[index];
                index = Incr(index, size);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #endregion
    }

    [Serializable]
    public class ConcurrentDeck<T> : IEnumerable<T>
    {
        private readonly int _size;
        private readonly T[] _buffer;
        private int _position = 0;

        public ConcurrentDeck(int size)
        {
            _size = size;
            _buffer = new T[size];
        }

        public int Size { get { return _size; } }

        public void Push(T item)
        {
            lock (this)
            {
                _buffer[_position] = item;
                _position++;
                if (_position == _size) _position = 0;
            }
        }

        public T[] ReadDeck()
        {
            lock (this)
            {
                return _buffer.Skip(_position).Union(_buffer.Take(_position)).Where(e => e != null).ToArray();
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.ReadDeck().AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.ReadDeck().GetEnumerator();
        }
    }

    //https://social.msdn.microsoft.com/Forums/vstudio/en-US/789c37ea-b9bf-4512-a418-f4f9532c59bf/dictionary-with-limited-size?forum=csharpgeneral

    [Serializable]
    public class LimitedDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        public ReadOnlyDictionary<TKey, TValue> AsReadOnly()
        {
            return new ReadOnlyDictionary<TKey, TValue>(this);
        }

        public int MaxItemsToHold { get; set; }

        private Queue<TKey> orderedKeys = new Queue<TKey>();

        public new void Add(TKey key, TValue value)
        {
            orderedKeys.Enqueue(key);
            if (this.MaxItemsToHold != 0 && this.Count >= MaxItemsToHold)
            {
                this.Remove(orderedKeys.Dequeue());
            }

            base.Add(key, value);
        }
    }

    [Serializable]
    public class LimitedSizeDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        Dictionary<TKey, TValue> dict;
        Queue<TKey> queue;
        int size;

        public LimitedSizeDictionary(int size)
        {
            this.size = size;
            dict = new Dictionary<TKey, TValue>(size + 1);
            queue = new Queue<TKey>(size);
        }

        public ReadOnlyDictionary<TKey, TValue> AsReadOnly()
        {
            return new ReadOnlyDictionary<TKey, TValue>(dict);
        }

        public void Add(TKey key, TValue value)
        {
            dict.Add(key, value);
            if (queue.Count == size)
                dict.Remove(queue.Dequeue());
            queue.Enqueue(key);
        }

        public bool Remove(TKey key)
        {
            if (dict.Remove(key))
            {
                Queue<TKey> newQueue = new Queue<TKey>(size);
                foreach (TKey item in queue)
                    if (!dict.Comparer.Equals(item, key))
                        newQueue.Enqueue(item);
                queue = newQueue;
                return true;
            }
            else
                return false;
        }

        public int Size { get { return size; } }

        public TValue this[TKey key]
        {
            get
            {
                return dict[key];
            }
            set
            {
                if (dict.ContainsKey(key)) dict[key] = value;
                else this.Add(key, value);
            }
        }

        public int Count
        {
            get
            {
                return dict.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return ((IDictionary<TKey, TValue>)dict).IsReadOnly;
            }
        }

        public ICollection<TKey> Keys
        {
            get
            {
                return dict.Keys;
            }
        }

        public ICollection<TValue> Values
        {
            get
            {
                return dict.Values;
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this.Add(item.Key, item.Value);
        }

        public void Clear()
        {
            dict.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return dict.Contains(item);
        }

        public bool ContainsKey(TKey key)
        {
            return dict.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)dict).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dict.GetEnumerator();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return dict.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return dict.GetEnumerator();
        }
    }

    #endregion 

}
