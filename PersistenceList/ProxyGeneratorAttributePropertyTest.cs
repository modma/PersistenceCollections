using Castle.DynamicProxy;
using Castle.DynamicProxy.Contributors;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace PersistenceList
{
    /// <summary>
    /// Generador de proxy rustico para añadir los artibutos de ProtoBuffer
    /// </summary>
    public class ProxyGeneratorAttributePropertyTest : Castle.DynamicProxy.Generators.ClassProxyGenerator
    {
        #region Overloads
        public ProxyGeneratorAttributePropertyTest(ModuleScope scope, Type targetType)
            : base(scope, targetType) { }

        protected override Type GenerateType(string name, Type[] interfaces, Castle.DynamicProxy.Generators.INamingScope namingScope)
        {
            IEnumerable<Castle.DynamicProxy.Contributors.ITypeContributor> contributors;
            var implementedInterfaces = GetTypeImplementerMapping(interfaces, out contributors, namingScope);

            //var model = new MetaType(name, targetType, implementedInterfaces);
            var model = new Castle.DynamicProxy.Generators.MetaType();

            // Collect methods
            foreach (var contributor in contributors)
            {
                contributor.CollectElementsToProxy(ProxyGenerationOptions.Hook, model);
            }

            ProxyGenerationOptions.Hook.MethodsInspected();

            var emitter = BuildClassEmitter(name, targetType, implementedInterfaces);

            CreateFields(emitter);
            CreateTypeAttributes(emitter);

            // Constructor
            var cctor = GenerateStaticConstructor(emitter);

            var constructorArguments = new List<Castle.DynamicProxy.Generators.Emitters.SimpleAST.FieldReference>();
            foreach (var contributor in contributors)
            {
                contributor.Generate(emitter, ProxyGenerationOptions);

                // TODO: redo it
                if (contributor is MixinContributor)
                {
                    constructorArguments.AddRange((contributor as MixinContributor).Fields);
                }
            }

            //inyecta la propiedad la serializacion personalizada de Protobuffer
            var pctor = typeof(ProtoBuf.ProtoMemberAttribute).GetConstructor(new[] { typeof(int) });
            foreach (var prop in model.Properties.Where(e => e.CanRead && e.CanWrite)
                .OrderBy(e => e.Getter.Name).Select((e, i) => new { e, i }))
            {
                // Create an attribute builder.
                object[] attributeArguments = new object[] { prop.i + 1 };
                var builder = new CustomAttributeBuilder(pctor, attributeArguments);
                prop.e.Emitter.DefineCustomAttribute(builder);
            }

            //inyecta la propiedad la serializacion personalizada de WCF
            pctor = typeof(DataMemberAttribute).GetConstructor(Type.EmptyTypes);
            foreach (var prop in model.Properties.Where(e => e.CanRead && e.CanWrite))
            {
                // Create an attribute builder.
                object[] attributeArguments = new object[0];
                var builder = new CustomAttributeBuilder(pctor, attributeArguments);
                prop.Emitter.DefineCustomAttribute(builder);
            }

            // constructor arguments
            var interceptorsField = emitter.GetField("__interceptors");
            constructorArguments.Add(interceptorsField);
            var selector = emitter.GetField("__selector");
            if (selector != null)
            {
                constructorArguments.Add(selector);
            }

            GenerateConstructors(emitter, targetType, constructorArguments.ToArray());
            GenerateParameterlessConstructor(emitter, targetType, interceptorsField);

            // Complete type initializer code body
            CompleteInitCacheMethod(cctor.CodeBuilder);

            // Crosses fingers and build type
            Type proxyType = emitter.BuildType();
            InitializeStaticFields(proxyType);
            return proxyType;
        }
        #endregion

        private static readonly Dictionary<Type, Type> MetaTypeCache = new Dictionary<Type, Type>();

        //http://patriksvensson.se/2013/08/how-to-dynamically-add-attributes-to-a-class-with-castle-core/
        public static Type CreateTypeDecoratedWithAttribute(Type classType)
        {
            if (classType == null || !classType.IsClass || classType.IsSealed) throw new InvalidOperationException("Invalid type");
            lock (MetaTypeCache)
            {
                Type typeProxified = null;
                if (!MetaTypeCache.TryGetValue(classType, out typeProxified))
                {
                    // Create the proxy generation options.
                    // This is how we tell Castle.DynamicProxy how to create the attribute.
                    var proxyOptions = new ProxyGenerationOptions();
                    var attrTypes = new [] { typeof(ProtoBuf.ProtoContractAttribute), typeof(DataContractAttribute) };

                    foreach (var attrType in attrTypes)
                    {
                        if (!classType.CustomAttributes.Any(e => e.AttributeType == attrType))
                        {
                            // Get the attribute constructor.
                            Type[] ctorTypes = Type.EmptyTypes;
                            var ctor = attrType.GetConstructor(ctorTypes);
                            Debug.Assert(ctor != null, "Could not get constructor for attribute.");

                            // Create an attribute builder.
                            object[] attributeArguments = new object[] { };
                            //var builder = new CustomAttributeBuilder(ctor, attributeArguments);
                            var builder = new CustomAttributeInfo(ctor, attributeArguments);
                            proxyOptions.AdditionalAttributes.Add(builder);
                        }
                    }

                    // Create the proxy generator.
                    var proxyGenerator = new ProxyGenerator();
                    //proxyGenerator.ProxyBuilder.CreateClassProxyType()
                    var newGenerator = new ProxyGeneratorAttributePropertyTest(proxyGenerator.ProxyBuilder.ModuleScope, classType) { Logger = proxyGenerator.ProxyBuilder.Logger };
                    MetaTypeCache.Add(classType, typeProxified = newGenerator.GenerateCode(Type.EmptyTypes, proxyOptions));
                }
                return typeProxified;
            }
        }

        public static T CreateClassDecoratedWithAttribute<T>() where T : class, new()
        {
            // Create the class proxy.
            var typeProxified = CreateTypeDecoratedWithAttribute(typeof(T));
            return (T)Activator.CreateInstance(typeProxified);
        }

    }
}
