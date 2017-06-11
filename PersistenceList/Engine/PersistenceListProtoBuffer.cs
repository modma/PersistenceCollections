using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ProtoBuf.Meta;

namespace PersistenceList
{
    [Serializable]
    public sealed class PersistenceListProtoBuffer<T> : PersistenceList<T>
    {
        private readonly CompressionMode useCompression;

        #region Contructors

        public PersistenceListProtoBuffer(bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : this(null, doSecure: doSecure, compression: compression, liveOpen: liveOpen)
        {
        }

        public PersistenceListProtoBuffer(System.IO.Stream connectionStream, bool doSecure = false, bool liveOpen = true, CompressionMode compression = CompressionMode.NoCompression)
            : base(connectionStream, doSecure, liveOpen)
        {
            useCompression = compression;
        }

        #endregion

        #region overrides

        protected override bool IsSerializable(Type type)
        {
            return RuntimeTypeModel.Default.CanSerialize(type);
        }

        protected override void Serialize(System.IO.Stream serializationStream, T graph)
        {
            Action<System.IO.Stream> fnSerializeBase = (compressionStream) => ProtoBuf.Serializer.Serialize(compressionStream ?? serializationStream, graph);
            Action<System.IO.Stream> fnSerialize = (useCompression == CompressionMode.NoCompression) ? fnSerializeBase :
                (compressionStream) => { using (compressionStream) fnSerializeBase(compressionStream); };

            switch (useCompression)
            {
                case CompressionMode.LZ4Fast:
                    fnSerialize(new Lz4Net.Lz4CompressionStream(serializationStream, Lz4Net.Lz4Mode.Fast, false)); break;
                case CompressionMode.LZ4Max:
                    fnSerialize(new Lz4Net.Lz4CompressionStream(serializationStream, Lz4Net.Lz4Mode.HighCompression, false)); break;
                case CompressionMode.DeflateFast:
                    fnSerialize(new System.IO.Compression.DeflateStream(serializationStream, System.IO.Compression.CompressionLevel.Fastest, true)); break;
                case CompressionMode.DeflateMax:
                    fnSerialize(new System.IO.Compression.DeflateStream(serializationStream, System.IO.Compression.CompressionLevel.Optimal, true)); break;
                case CompressionMode.GZipFast:
                    fnSerialize(new System.IO.Compression.GZipStream(serializationStream, System.IO.Compression.CompressionLevel.Fastest, true)); break;
                case CompressionMode.GzipMax:
                    fnSerialize(new System.IO.Compression.GZipStream(serializationStream, System.IO.Compression.CompressionLevel.Optimal, true)); break;
                default:
                    fnSerialize(null); break;
            }
        }
        
        protected override T Deserialize(System.IO.Stream serializationStream)
        {
            Func<System.IO.Stream, T> fnDeserialize = (compressionStream) => { using (compressionStream) return ProtoBuf.Serializer.Deserialize<T>(compressionStream); };
            switch (useCompression)
            {
                case CompressionMode.LZ4Fast:
                case CompressionMode.LZ4Max:
                    return fnDeserialize(new Lz4Net.Lz4DecompressionStream(serializationStream, true));
                case CompressionMode.DeflateFast:
                case CompressionMode.DeflateMax:
                    return fnDeserialize(new System.IO.Compression.DeflateStream(serializationStream, System.IO.Compression.CompressionMode.Decompress, false));
                case CompressionMode.GZipFast:
                case CompressionMode.GzipMax:
                    return fnDeserialize(new System.IO.Compression.GZipStream(serializationStream, System.IO.Compression.CompressionMode.Decompress, false));
                default:
                    return fnDeserialize(serializationStream);
            }
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
        }

        #region MetaDecoratorProtoBuffer

        public static MetaType MetaDecorator()
        {
            return MetaDecoratorProtoBuffer.Decorate(typeof(T));
        }
        
        #endregion

    }

    internal sealed class MetaDecoratorProtoBuffer
    {
        //Based on: https://stackoverflow.com/questions/17201571/protobuf-net-serialization-without-attributes
        private static readonly Dictionary<Type, MetaType> MetaTypeCache = new Dictionary<Type, MetaType>();

        public static MetaType Decorate(Type type)
        {
            lock (MetaTypeCache)
            {
                MetaType mtc = null;
                if (!MetaTypeCache.TryGetValue(type, out mtc) && !RuntimeTypeModel.Default.CanSerialize(type))
                {
                    var types = GetParentTypes(type).Concat(new[] { type }).Where(e => !e.IsInterface);
                    var fieldsGroup = types.Select(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
                        .SelectMany(t => t).Where(t => !MetaTypeCache.ContainsKey(t.DeclaringType)).Distinct().GroupBy(e => e.DeclaringType);

                    if (fieldsGroup.Any())
                    {
                        foreach (var fields in fieldsGroup)
                        {
                            if (!RuntimeTypeModel.Default.CanSerialize(fields.Key))
                            {
                                MetaTypeCache.Add(fields.Key, mtc = RuntimeTypeModel.Default.Add(fields.Key, false));
                                var vms = fields.OrderBy(e => e.Name).Select((f, i) => mtc.AddField(i + 1, f.Name)).ToList();
                                vms.ForEach(vm => Decorate(vm.ItemType ?? vm.DefaultType ?? vm.MemberType)); //preprocess inside vm
                            }
                        }
                    }
                    else MetaTypeCache.Add(type, RuntimeTypeModel.Default.Add(type, true));
                    if (!MetaTypeCache.TryGetValue(type, out mtc)) return null;
                }
                return mtc;
            }
        }

        public static IEnumerable<Type> GetParentTypes(Type type)
        {
            // not null type
            if (type == null)
            {
                yield break;
            }

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
    }

    public enum CompressionMode
    {
        NoCompression,
        LZ4Fast,
        LZ4Max,
        DeflateFast,
        DeflateMax,
        GZipFast,
        GzipMax
    }

}
