using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListProtoMessage<T> : PersistenceList<T> where T : IMessage<T>, new()
    {
        private MessageParser<T> formatter = new MessageParser<T>(() => new T());

        #region Contructors

        public PersistenceListProtoMessage(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListProtoMessage(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            return typeof(IMessage).IsAssignableFrom(type) && (typeof(T).IsAssignableFrom(type) || typeof(T) == type);
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            if (graph != null) graph.WriteTo(serializationStream);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            return formatter.ParseFrom(serializationStream);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
