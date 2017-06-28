using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListNewtonsoft<T> : PersistenceList<T>
    {
        private JsonSerializer formatter = new JsonSerializer();

        private const int BUFFER = 1024;
        private static Encoding encoding;

        public static Encoding DefaultEncoding
        {
            get
            {
                if (encoding == null)
                {
                    using (MemoryStream ms = new MemoryStream())
                    using (StreamWriter writer = new StreamWriter(ms))
                        encoding = writer.Encoding; //default encoding
                }
                return encoding;
            }
        }

        #region Contructors

        public PersistenceListNewtonsoft(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure, liveOpen, compression)
        {
        }

        public PersistenceListNewtonsoft(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
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
            using (StreamWriter writer = new StreamWriter(serializationStream, DefaultEncoding, BUFFER, true))
            using (JsonTextWriter jsonWriter = new JsonTextWriter(writer))
            {
                formatter.Serialize(jsonWriter, graph);
                jsonWriter.Flush();
            }
        }

        protected override T Deserialize(Stream serializationStream)
        {
            if (formatter == null) throw new ObjectDisposedException(nameof(formatter));
            using (StreamReader reader = new StreamReader(serializationStream))
            using (JsonTextReader jsonReader = new JsonTextReader(reader))
            {
                return formatter.Deserialize<T>(jsonReader);
            }
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
