using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListXML<T> : PersistenceList<T>
    {
        private XmlSerializer formatter = null;

        #region Contructors

        public PersistenceListXML(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListXML(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            return (type != null && type.IsSerializable);
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            if (formatter == null) formatter = new XmlSerializer(typeof(T));
            formatter.Serialize(serializationStream, graph);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            if (formatter == null) formatter = new XmlSerializer(typeof(T));
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
