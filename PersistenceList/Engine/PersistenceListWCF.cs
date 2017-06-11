using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListWCF<T> : PersistenceList<T>
    {
        private DataContractSerializer formatter = null;

        protected override bool IsSerializable(Type type)
        {
            try { return new DataContractSerializer(type) != null; }
            catch { return false; }
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            if (formatter == null) formatter = new DataContractSerializer(typeof(T));
            formatter.WriteObject(serializationStream, graph);
        }

        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            if (formatter == null) formatter = new DataContractSerializer(typeof(T));
            return (T)formatter.ReadObject(serializationStream);
        }

        public override void Dispose()
        {
            base.Dispose();
            formatter = null;
        }

    }
}
