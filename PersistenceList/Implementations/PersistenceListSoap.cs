using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Soap;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListSoap<T> : PersistenceList<T>
    {
        private SoapFormatter formatter = new SoapFormatter(null,
                new StreamingContext(StreamingContextStates.Persistence));

        #region Contructors

        public PersistenceListSoap(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListSoap(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            return (type != null && (type.IsSerializable || GetParentTypes(type).Contains(typeof(ISerializable))));
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            if (formatter == null) throw new ObjectDisposedException(nameof(formatter));
            formatter.Serialize(serializationStream, graph);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            if (formatter == null) throw new ObjectDisposedException(nameof(formatter));
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
