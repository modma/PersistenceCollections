using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListJSON<T> : PersistenceList<T>
    {
        private DataContractJsonSerializer formatter = null;

        #region Contructors

        public PersistenceListJSON(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListJSON(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            try { return new DataContractJsonSerializer(type) != null; }
            catch { return false; }
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            if (formatter == null) formatter = new DataContractJsonSerializer(typeof(T));
            formatter.WriteObject(serializationStream, graph);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            if (formatter == null) formatter = new DataContractJsonSerializer(typeof(T));
            return (T)formatter.ReadObject(serializationStream);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
