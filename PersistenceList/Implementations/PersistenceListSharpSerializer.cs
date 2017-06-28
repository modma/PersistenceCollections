using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListSharpSerializer<T> : PersistenceList<T>
    {
        private Polenter.Serialization.SharpSerializer formatter = null;

        #region Contructors

        public PersistenceListSharpSerializer(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression, bool binarySerialization = false)
            : this(null, doSecure, liveOpen, compression, binarySerialization)
        {
        }

        public PersistenceListSharpSerializer(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression, bool binarySerialization = false)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
            formatter = !binarySerialization ? new Polenter.Serialization.SharpSerializer(false) :
                new Polenter.Serialization.SharpSerializer(new Polenter.Serialization.SharpSerializerBinarySettings(Polenter.Serialization.BinarySerializationMode.Burst));
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            try { return (formatter ?? new Polenter.Serialization.SharpSerializer()) //temporal instance if null
                    .PropertyProvider.GetProperties(new Polenter.Serialization.Serializing.TypeInfo() { Type = type })?.Count > 0; }
            catch { return false; }
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            formatter.Serialize(graph, serializationStream);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            return (T)formatter.Deserialize(serializationStream);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
