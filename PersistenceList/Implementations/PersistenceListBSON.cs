using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListBSON<T> : PersistenceList<T>
    {
        private JsonSerializer formatter = new JsonSerializer();

        #region Contructors

        public PersistenceListBSON(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListBSON(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            try { return formatter.ContractResolver.ResolveContract(type) != null; }
            catch { return false; }
        }

        protected override void Serialize(Stream serializationStream, T graph)
        {
            if (formatter == null) throw new ObjectDisposedException(nameof(formatter));
            using (var writer = new BsonDataWriter(serializationStream))
            {
                formatter.Serialize(writer, graph);
                writer.Flush();
            }
        }

        protected override T Deserialize(Stream serializationStream)
        {
            if (formatter == null) throw new ObjectDisposedException(nameof(formatter));
            using (var reader = new BsonDataReader(serializationStream))
                return formatter.Deserialize<T>(reader);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
