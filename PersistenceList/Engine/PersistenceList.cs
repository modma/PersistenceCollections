using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Data;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace PersistenceList
{

    [DebuggerTypeProxy(typeof(PersistenceList_CollectionDebugView<>))]
    [DebuggerDisplay("Count = {Count}")][Serializable]
    public class PersistenceList<T> : IList<T>, IDisposable
    {
        #region Fields
        public const int MAX_SQLITE_COMMAND_PARAMETERS = 999; //limit 999 sqlite command parameters
        private const int MAX_DATASIZE_BULK_DUMP = 2097152; //2MB max in mode per dump
        private readonly System.Security.SecureString password = null;
        private readonly string temporalFile = null;
        private readonly object locker = new object();
        private readonly static TempFileCollection tfc = null;
        protected readonly CompressionMode useCompression;

        //Persistent Conection
        private readonly bool liveOpen;
        private SQLiteConnection liveCnn;
        #endregion

        #region Contructors

        static PersistenceList()
        {
            tfc = new TempFileCollection() { KeepFiles = false };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => tfc.Delete();
            //GC.KeepAlive(tfc);
        }

        public PersistenceList(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceList(Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
        {
            if (!this.IsSerializable(typeof(T))) throw new System.Runtime.Serialization.SerializationException("The class is not serializable");

            temporalFile = Path.GetTempFileName();
            tfc.AddFile(temporalFile, false);
            this.liveOpen = liveOpen;
            this.useCompression = compression;

            if (doSecure)
            {
                this.password = new System.Security.SecureString();
                Enumerable.Concat(Path.GetRandomFileName(), Path.GetRandomFileName().Reverse())
                    .ToList().ForEach(password.AppendChar);
                password.MakeReadOnly();
            }

            if (connectionStream != null)
            {
                using (FileStream fileStream = new FileStream(temporalFile, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
                {
                    connectionStream.CopyTo(fileStream);
                    fileStream.Flush();
                }
            }
        }

        #endregion

        #region Tools

        private string GetConnectionString
        {
            get { return "Data Source=" + temporalFile + ";Version=3;"; }
        }

        public static byte[] GetBinary(string filePath)
        {
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] binary = GetBinary(stream);
                return binary;
            }
        }

        public static byte[] GetBinary(Stream stream)
        {
            if (stream == null) return null;
            using (BinaryReader reader = new BinaryReader(stream))
            {
                byte[] binary = reader.ReadBytes((int)stream.Length);
                return binary;
            }
        }

        //http://stackoverflow.com/questions/8868119/find-all-parent-types-both-base-classes-and-interfaces
        public static IEnumerable<Type> GetParentTypes(Type type = null)
        {
            // if null type, use the Genetric Type
            if (type == null) type = typeof(T);

            // return all implemented or inherited interfaces
            foreach (var i in type.GetInterfaces())
            {
                yield return i;
            }

            // return all inherited types
            var currentBaseType = type.BaseType;
            while (currentBaseType != null)
            {
                yield return currentBaseType;
                currentBaseType = currentBaseType.BaseType;
            }
        }

        private Tout MakeConnection<Tout>(Func<SQLiteConnection, Tout> execute)
        {
            if (_disposed) throw new ObjectDisposedException("Container");
            lock (locker)
            {
                if (!liveOpen || liveCnn == null || liveCnn.State == ConnectionState.Broken || liveCnn.State == ConnectionState.Closed)
                {
                    liveCnn = new SQLiteConnection(this.GetConnectionString);
                    this.SetConnectionPassword(liveCnn);
                }
                if (liveCnn.State != ConnectionState.Open) liveCnn.Open();
                var ret = execute(liveCnn);
                if (!liveOpen) { liveCnn.Dispose(); liveCnn = null; }
                return ret;
            }
        }

        private void SetConnectionPassword(SQLiteConnection connection)
        {
            if (connection != null && this.password != null)
            {
                IntPtr unmanagedString = IntPtr.Zero;
                try
                {
                    unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(this.password);
                    connection.SetPassword(Marshal.PtrToStringUni(unmanagedString));
                }
                finally
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        public void BackupDatabase(SQLiteConnection destination, string destinationName, string sourceName, int pages = -1, SQLiteBackupCallback callback = null, int retryMilliseconds = -1)
        {
            MakeConnection(connection =>
            {
                connection.BackupDatabase(destination, destinationName, sourceName, pages, callback, retryMilliseconds);
                return true;
            });
        }

        #endregion

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string TemporalFile { get { return temporalFile; } }

        public ReadOnlyCollection<T> AsReadOnly()
        {
            return new ReadOnlyCollection<T>(this);
        }

        #region Manager
        private bool isInit = false;
        private const string TempDBTable = "tempDB";
        private const string WorkingDBTable = "datastore";
        private const string DeleteCommand = "DELETE FROM datastore WHERE id=@id;";
        private const string SelectCommand = "SELECT data FROM datastore WHERE id=@id;";
        private const string CreateBaseTable = "CREATE TABLE IF NOT EXISTS {0} (id INTEGER PRIMARY KEY, data BLOB NULL);";

        public void Inicialize()
        {
            MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = string.Format(CreateBaseTable, WorkingDBTable);
                    int result = command.ExecuteNonQuery();
                    return result;
                }
            });
            isInit = true;
        }

        #region Operations

        #region Store Methods

        private void StoreObject(int? key, T obj, bool insertAt)
        {
            if (object.ReferenceEquals(obj, null))
            {
                this.StoreData(key, (Stream)null, insertAt);
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    this.DeltaCreate(ms, obj);
                    ms.Seek(0, SeekOrigin.Begin);
                    this.StoreData(key, ms, insertAt);
                }
            }
        }

        private void StoreData(int? key, string filePath, bool insertAt)
        {
            this.StoreData(key, GetBinary(filePath), insertAt);
        }

        private void StoreData(int? key, Stream stream, bool insertAt)
        {
            this.StoreData(key, GetBinary(stream), insertAt);
        }

        private void StoreData(int? key, byte[] data, bool insertAt)
        {
            if (!key.HasValue && insertAt) throw new InvalidOperationException();
            if (!isInit) Inicialize();
            MakeConnection(connection =>
            {
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        bool canInsert = true;
                        command.CommandType = System.Data.CommandType.Text;
                        if (key.HasValue)
                        {
                            command.Parameters.Add(new SQLiteParameter("@id", key.Value + 1));
                            if (insertAt)
                            {
                                if (canInsert = !(key.Value < 0 || key.Value > this.Count))
                                {
                                    command.CommandText = string.Format(CreateBaseTable + //reindex
                                    "\n INSERT INTO {0} (id, data)\n SELECT (CASE WHEN id >= @id THEN id+1 ELSE id END) id, data FROM {1};" +
                                    "\n DROP TABLE {1};\n ALTER TABLE {0} RENAME TO {1};", TempDBTable, WorkingDBTable);
                                    var test = command.ExecuteNonQuery();
                                }
                                command.CommandText = "INSERT INTO datastore VALUES(@id, @data);"; //insert at
                            }
                            else
                            {
                                command.CommandText = "UPDATE datastore SET data = @data WHERE id = @id;"; //update
                            }
                        }
                        else command.CommandText = "INSERT INTO datastore VALUES(null, @data);"; //insert
                        command.Parameters.Add(new SQLiteParameter("@data", object.ReferenceEquals(data, null) ? (object)DBNull.Value : data));
                        int result = canInsert ? command.ExecuteNonQuery() : 0;
                        command.Parameters.Clear();
                        if (result != 1 && key.HasValue) throw new ArgumentOutOfRangeException(nameof(key));
                        transaction.Commit();
                        return result;
                    }
                }
            });
        }

        #endregion

        #region Get Methods

        private byte[] GetData(int key)
        {
            if (!isInit) Inicialize();
            return MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = SelectCommand;
                    command.Parameters.Add(new SQLiteParameter("@id", key + 1));
                    object result = command.ExecuteScalar() ?? DBNull.Value;
                    return object.Equals(result, DBNull.Value) ? null : (byte[])result;
                }
            });
        }

        private Stream GetStream(int key)
        {
            if (!isInit) Inicialize();
            return MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = SelectCommand;
                    command.Parameters.Add(new SQLiteParameter("@id", key + 1));
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    Stream stream = reader.GetStream(0);
                                    if (stream != null && stream.Length != 0) return stream;
                                    if (stream != null) stream.Dispose();
                                }
                            }
                        }
                        else throw new IndexOutOfRangeException();
                        return null;
                    }
                }
            });
        }

        private T GetObject(int key)
        {
            using (Stream ms = this.GetStream(key))
            {
                if (ms == null || ms.Length == 0) return default(T);
                return this.DeltaApply(ms);
            }
        }

        #endregion

        private bool RemoveData(int key)
        {
            if (!isInit) Inicialize();
            return MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = DeleteCommand;
                    command.Parameters.Add(new SQLiteParameter("@id", key + 1));
                    int result = command.ExecuteNonQuery();
                    if (result > 0)
                    {
                        command.CommandText = "UPDATE datastore SET id = id-1 WHERE id > @id;";//reindex
                        result = command.ExecuteNonQuery();
                        return true;
                    }
                    return false;
                }
            });
        }

        private void DeltaCreate(Stream deltaStream, T graph)
        {
            if (useCompression == CompressionMode.NoCompression) this.Serialize(deltaStream, graph);
            else using (var compressionStream = DoCompress(deltaStream)) this.Serialize(compressionStream, graph);
        }

        private T DeltaApply(Stream deltaStream)
        {
            using (var compressionStream = DoDecompress(deltaStream)) return this.Deserialize(compressionStream);
        }

        public DataTable ListElements()
        {
            if (!isInit) Inicialize();
            return MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = "SELECT id-1, LENGTH(data) as lenght FROM datastore";
                    using (var reader = command.ExecuteReader())
                    {
                        DataTable result = new DataTable();
                        result.Load(reader);
                        return result;
                    }
                }
            });
        }

        #endregion

        #endregion

        #region Serialization

        private System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = null;

        protected virtual bool IsSerializable(Type type)
        {
            return (type != null && type.IsSerializable);
        }

        protected virtual void Serialize(Stream serializationStream, T graph)
        {
            if (formatter == null) formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            formatter.Serialize(serializationStream, graph);
        }

        protected virtual T Deserialize(Stream serializationStream)
        {
            if (formatter == null) formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            return (T)formatter.Deserialize(serializationStream);
        }

        #endregion

        #region Compression

        private Stream DoCompress(Stream input)
        {
            switch (useCompression)
            {
                case CompressionMode.LZ4Fast:
                    return new Lz4Net.Lz4CompressionStream(input, Lz4Net.Lz4Mode.Fast, false);
                case CompressionMode.LZ4Max:
                    return new Lz4Net.Lz4CompressionStream(input, Lz4Net.Lz4Mode.HighCompression, false);
                case CompressionMode.DeflateFast:
                    return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionLevel.Fastest, true);
                case CompressionMode.DeflateMax:
                    return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionLevel.Optimal, true);
                case CompressionMode.GZipFast:
                    return new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionLevel.Fastest, true);
                case CompressionMode.GzipMax:
                    return new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionLevel.Optimal, true);
                case CompressionMode.Custom:
                    return CustomCompress(input);
                default:
                    return input;
            }
        }

        protected virtual Stream CustomCompress(Stream input)
        {
            throw new NotImplementedException("Must be override to use");
        }

        private Stream DoDecompress(Stream input)
        {
            switch (useCompression)
            {
                case CompressionMode.LZ4Fast:
                case CompressionMode.LZ4Max:
                    return new Lz4Net.Lz4DecompressionStream(input, true);
                case CompressionMode.DeflateFast:
                case CompressionMode.DeflateMax:
                    return new System.IO.Compression.DeflateStream(input, System.IO.Compression.CompressionMode.Decompress, false);
                case CompressionMode.GZipFast:
                case CompressionMode.GzipMax:
                    return new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress, false);
                case CompressionMode.Custom:
                    return CustomDecompress(input);
                default:
                    return input;
            }
        }

        protected virtual Stream CustomDecompress(Stream input)
        {
            throw new NotImplementedException("Must be override to use");
        }

        #endregion

        #region Miembros de IDisposable

        private bool _disposed = false;
        private void Dispose(bool disposing)
        {
            // If you need thread safety, use a lock around these 
            // operations, as well as in your methods that use the resource.
            if (!_disposed)
            {
                if (disposing)
                {
                    if (liveCnn != null) try { liveCnn.Dispose(); } finally { liveCnn = null; }
                    FileInfo file = new FileInfo(temporalFile);
                    if (file.Exists) file.Delete();
                    if (this.password != null) this.password.Dispose();
                    this.formatter = null;
                }
                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
        }

        #endregion

        #region IList Implementation
        public int IndexOf(T item)
        {
            if (!isInit) Inicialize();
            return MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    if (object.ReferenceEquals(item, null))
                    {
                        command.CommandText = "SELECT id-1 from datastore WHERE data is null ORDER BY id;";
                    }
                    else
                    {
                        command.CommandText = "SELECT id-1 from datastore WHERE data = @data ORDER BY id;";
                        using (var ms = new MemoryStream())
                        {
                            this.DeltaCreate(ms, item);
                            //ms.Seek(0, SeekOrigin.Begin);
                            command.Parameters.Add(new SQLiteParameter("@data", ms.ToArray()));
                        }
                    }
                    object result = command.ExecuteScalar() ?? DBNull.Value;
                    int ret = object.Equals(result, DBNull.Value) ? -1 : Convert.ToInt32(result);
                    return ret;
                }
            });
        }

        public void Insert(int index, T item)
        {
            StoreObject(index, item, true);
        }

        public void RemoveAt(int index)
        {
            RemoveData(index);
        }

        public T this[int index]
        {
            get
            {
                return GetObject(index);
            }
            set
            {
                StoreObject(index, value, false);
            }
        }

        public void Add(T item)
        {
            StoreObject(null, item, false);
        }

        public void AddRange(IEnumerable<T> items, bool bySize = true)
        {
            if (items == null) return;
            if (!isInit) Inicialize();
            MakeConnection(connection =>
            {
                Func<int, string> MakeCommand = (len) => string.Concat("INSERT INTO datastore VALUES",
                    String.Join(",", Enumerable.Range(0, len).Select(n => string.Concat("(null, @data", n, ")")))
                    , ";");

                #region By size or parameter limit
                if (bySize)
                {
                    using (var command = connection.CreateCommand())
                    {
                        int arrayIndex = 0;
                        long dataSize = 0;
                        command.CommandType = CommandType.Text;
                        Func<int> Flush = () =>
                        {
                            command.CommandText = MakeCommand(arrayIndex);
                            int result = command.ExecuteNonQuery();
                            dataSize = arrayIndex = 0;
                            command.Parameters.Clear();
                            return result;
                        };

                        using (var numerator = items.GetEnumerator())
                        {
                            while (numerator.MoveNext())
                            {
                                string variable = "@data" + arrayIndex;
                                var e = numerator.Current;

                                if (object.ReferenceEquals(e, null))
                                {
                                    command.Parameters.Add(new SQLiteParameter(variable, DBNull.Value));
                                }
                                else
                                {
                                    using (var ms = new MemoryStream())
                                    {
                                        this.DeltaCreate(ms, e);
                                        var buffer = ms.ToArray();
                                        command.Parameters.Add(new SQLiteParameter(variable, buffer));
                                        dataSize += buffer.Length;
                                    }
                                }
                                arrayIndex++;
                                if (MAX_SQLITE_COMMAND_PARAMETERS <= arrayIndex || dataSize >= MAX_DATASIZE_BULK_DUMP) Flush(); //limit 999 sqlite / 2MB
                            }
                            if (arrayIndex != 0) Flush();
                        }
                    }
                }
                #endregion
                #region By chunk size
                else
                {
                    int oldLength = 0;
                    string commandText = null;
                    foreach (var chunk in BufferCollection<T>.Partition(items, 256))
                    {
                        if (chunk.Length != 0)
                        {
                            if (chunk.Length != oldLength)
                                commandText = MakeCommand(oldLength = chunk.Length);

                            using (var command = connection.CreateCommand())
                            {
                                command.CommandType = CommandType.Text;
                                command.CommandText = commandText;
                                command.Parameters.AddRange(chunk.Select((e, n) =>
                                {
                                    string variable = "@data" + n.ToString();
                                    if (object.ReferenceEquals(e, null))
                                    {
                                        return new SQLiteParameter(variable, DBNull.Value);
                                    }
                                    else
                                    {
                                        using (var ms = new MemoryStream())
                                        {
                                            this.DeltaCreate(ms, e);
                                            //ms.Seek(0, SeekOrigin.Begin);
                                            return new SQLiteParameter(variable, ms.ToArray());
                                        }
                                    }
                                }).ToArray());
                                int result = command.ExecuteNonQuery();
                                command.Parameters.Clear();
                            }
                        }
                    }
                }
                #endregion

                return true;
            });
        }

        public BufferCollection<T> MakeBufferedAdd(bool bySize = true, bool useTask = false)
        {
            return new BufferCollection<T>(MAX_SQLITE_COMMAND_PARAMETERS, e => this.AddRange(e, bySize), useTask);
        }

        public void ForEach(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException();
            using (var numerator = this.GetEnumerator())
            {
                while (numerator.MoveNext())
                {
                    action(numerator.Current);
                }
            }
        }

        public void Clear()
        {
            if (!isInit) Inicialize();
            MakeConnection(connection =>
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandType = System.Data.CommandType.Text;
                    command.CommandText = "DELETE from datastore WHERE 1=1;";
                    int result = command.ExecuteNonQuery();
                    return result;
                }
            });
        }

        public bool Contains(T item)
        {
            int index = this.IndexOf(item);
            return index != -1;
        }

        public void CopyTo(T[] array, int arrayIndex)
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

        public int Count
        {
            get
            {
                if (!isInit) Inicialize();
                return MakeConnection(connection =>
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandType = System.Data.CommandType.Text;
                        command.CommandText = "SELECT count(*) from datastore;";
                        int result = Convert.ToInt32(command.ExecuteScalar());
                        return result;
                    }
                });
            }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(T item)
        {
            int index = this.IndexOf(item);
            if (index != -1) { RemoveAt(index); return true; }
            else return false;
        }

        #region IEnumerator

        public IEnumerator<T> GetEnumerator()
        {
            if (!isInit) Inicialize();
            return this.SequenceForEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        private IEnumerator<T> SequenceForEnumerator()
        {
            if (_disposed) throw new ObjectDisposedException("Container");
            lock (locker) //MakeConnection copy!
            {
                try
                {
                    if (!liveOpen || liveCnn == null || liveCnn.State == ConnectionState.Broken || liveCnn.State == ConnectionState.Closed)
                    {
                        liveCnn = new SQLiteConnection(this.GetConnectionString);
                        this.SetConnectionPassword(liveCnn);
                    }
                    if (liveCnn.State != ConnectionState.Open) liveCnn.Open();
                    using (var command = liveCnn.CreateCommand())
                    {
                        command.CommandType = System.Data.CommandType.Text;
                        command.CommandText = "SELECT data FROM datastore ORDER BY id";
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (reader.IsDBNull(0)) yield return default(T);
                                else using (var ms = reader.GetStream(0))
                                        yield return this.DeltaApply(ms);
                            }
                        }
                    }
                }
                finally
                {
                    if (!liveOpen) { liveCnn.Dispose(); liveCnn = null; }
                }

            }
        }

        #endregion

        #endregion
    }

    public enum CompressionMode
    {
        NoCompression,
        Custom,
        LZ4Fast,
        LZ4Max,
        DeflateFast,
        DeflateMax,
        GZipFast,
        GzipMax
    }


    #region Debug View
    internal sealed class PersistenceList_CollectionDebugView<T>
    {
        private ICollection<T> collection;

        public PersistenceList_CollectionDebugView(ICollection<T> collection)
        {
            if (collection == null)
            {
                throw new Exception("ExceptionArgument.collection");
            }
            this.collection = collection;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items
        {
            get
            {
                T[] array = new T[this.collection.Count];
                this.collection.CopyTo(array, 0);
                return array;
            }
        }
    }
    #endregion
}