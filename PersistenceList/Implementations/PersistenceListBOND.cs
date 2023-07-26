using Bond;
using Bond.IO.Unsafe;
using Bond.Protocols;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListBOND<T> : PersistenceList<T>
    {
        private readonly bool isFast;

        #region Contructors

        public PersistenceListBOND(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression, bool fast = true)
            : this(null, doSecure, liveOpen, compression, fast)
        {
        }

        public PersistenceListBOND(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression, bool fast = true)
            : base(connectionStream, doSecure, liveOpen, compression)
        {
            isFast = fast;
        }

        #endregion

        #region Overrides

        protected override bool IsSerializable(Type type)
        {
            if (Attribute.IsDefined(type, typeof(SchemaAttribute), inherit: true))
            {
                try { var schema = Schema.GetRuntimeSchema(type); return true; }
                catch (Exception ex) { Console.WriteLine(ex.Message); }
            }
            return false;
        }

        protected override void Serialize(Stream serializationStream, T graph)
        {
            IProtocolWriter writer;
            var output = new OutputStream(serializationStream);
            if (isFast) writer = new FastBinaryWriter<OutputStream>(output);
            else writer = new CompactBinaryWriter<OutputStream>(output);
            Bond.Serialize.To(writer, graph);
        }

        protected override T Deserialize(Stream serializationStream)
        {
            ITaggedProtocolReader reader;
            var input = new InputStream(serializationStream);
            if (isFast) reader = new FastBinaryReader<InputStream>(input);
            else reader = new CompactBinaryReader<InputStream>(input);
            return Bond.Deserialize<T>.From(reader);
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }

    }
}
