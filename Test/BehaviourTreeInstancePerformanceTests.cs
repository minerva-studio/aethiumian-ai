#nullable enable
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Tests
{
    public sealed class BehaviourTreeInstancePerformanceTests
    {
        private const int NodeCount = 1000;
        private const int WarmupCount = 3;
        private const int MeasurementCount = 20;
        private const float InitializationTimeoutSeconds = 10f;

        private static readonly SampleGroup GeneratedInstanceTime = new(
            "BehaviourTree instance creation - generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup FallbackInstanceTime = new(
            "BehaviourTree instance creation - fallback accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup GeneratedCloneTime = new(
            "TreeNode clone only - generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup FallbackCloneTime = new(
            "TreeNode clone only - fallback accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup GeneratedAccessorEnumerationTime = new(
            "TreeNode accessor enumeration - generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup FallbackAccessorEnumerationTime = new(
            "TreeNode accessor enumeration - fallback accessors",
            SampleUnit.Millisecond);

        private static readonly ConcurrentDictionary<Type, IReadOnlyList<NodeReferenceAccessor>> NodeReferenceAccessorCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<NodeReferenceCollectionAccessor>> NodeReferenceCollectionAccessorCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<VariableAccessor>> VariableAccessorCache = new();
        private static readonly ConcurrentDictionary<Type, NodeAccessor> RealFallbackAccessorCache = new();
        private static readonly ConcurrentDictionary<Type, NodeAccessor> RealGeneratedAccessorCache = new();
        private static int benchmarkSink;

        [UnityTest, Performance]
        public IEnumerator CreateBehaviourTreeInstance_GeneratedAccessorsVsFallbackAccessors()
        {
            DynamicTreeFixture generated = DynamicTreeFixture.Create(includeRegistry: true, NodeCount);
            DynamicTreeFixture fallback = DynamicTreeFixture.Create(includeRegistry: false, NodeCount);
            List<double> generatedSamples = new();
            List<double> fallbackSamples = new();

            // Warm up both code paths so accessor lookup and expression compilation are not measured.
            for (int i = 0; i < WarmupCount; i++)
            {
                yield return CreateRuntimeTree(generated, recordSample: false, GeneratedInstanceTime, generatedSamples);
                yield return CreateRuntimeTree(fallback, recordSample: false, FallbackInstanceTime, fallbackSamples);
            }

            for (int i = 0; i < MeasurementCount; i++)
            {
                yield return CreateRuntimeTree(generated, recordSample: true, GeneratedInstanceTime, generatedSamples);
                yield return CreateRuntimeTree(fallback, recordSample: true, FallbackInstanceTime, fallbackSamples);
            }

            WriteSummary("BehaviourTree instance creation", generatedSamples, fallbackSamples);
            generated.Dispose();
            fallback.Dispose();
        }

        [Test, Performance]
        public void CloneNodes_GeneratedAccessorsVsFallbackAccessors()
        {
            using DynamicTreeFixture generated = DynamicTreeFixture.Create(includeRegistry: true, NodeCount);
            using DynamicTreeFixture fallback = DynamicTreeFixture.Create(includeRegistry: false, NodeCount);
            List<double> generatedSamples = new();
            List<double> fallbackSamples = new();

            Warmup(() => CloneAllNodes(generated));
            Warmup(() => CloneAllNodes(fallback));

            for (int i = 0; i < MeasurementCount; i++)
            {
                MeasureAction(generatedSamples, GeneratedCloneTime, () => CloneAllNodes(generated));
                MeasureAction(fallbackSamples, FallbackCloneTime, () => CloneAllNodes(fallback));
            }

            WriteSummary("TreeNode clone only", generatedSamples, fallbackSamples);
        }

        [Test, Performance]
        public void EnumerateAccessors_GeneratedAccessorsVsFallbackAccessors()
        {
            TreeNode[] nodes = CreateRealAccessorFixtureNodes();
            List<double> generatedSamples = new();
            List<double> fallbackSamples = new();

            Warmup(() => EnumerateAccessorFields(nodes, GetGeneratedAccessor));
            Warmup(() => EnumerateAccessorFields(nodes, GetFallbackAccessor));

            for (int i = 0; i < MeasurementCount; i++)
            {
                MeasureAction(generatedSamples, GeneratedAccessorEnumerationTime, () => EnumerateAccessorFields(nodes, GetGeneratedAccessor));
                MeasureAction(fallbackSamples, FallbackAccessorEnumerationTime, () => EnumerateAccessorFields(nodes, GetFallbackAccessor));
            }

            WriteSummary("TreeNode accessor enumeration on real nodes", generatedSamples, fallbackSamples);
        }

        private static void Warmup(System.Action action)
        {
            for (int i = 0; i < WarmupCount; i++)
            {
                action();
            }
        }

        private static void MeasureAction(List<double> samples, SampleGroup sampleGroup, System.Action action)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();

            double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
            samples.Add(milliseconds);
            Measure.Custom(sampleGroup, milliseconds);
        }

        private static void CloneAllNodes(DynamicTreeFixture fixture)
        {
            int sink = 0;
            foreach (TreeNode node in fixture.Data.nodes)
            {
                TreeNode clone = NodeFactory.Duplicate(node);
                sink ^= clone.uuid.GetHashCode();
            }

            benchmarkSink ^= sink;
        }

        private static void EnumerateAccessorFields(TreeNode[] nodes, Func<Type, NodeAccessor> getAccessor)
        {
            int sink = 0;

            foreach (TreeNode node in nodes)
            {
                NodeAccessor accessor = getAccessor(node.GetType());
                foreach (NodeReferenceAccessor nodeReferenceAccessor in accessor.NodeReferences)
                {
                    INodeReference reference = nodeReferenceAccessor.Get(node);
                    sink ^= reference?.UUID.GetHashCode() ?? 0;
                }

                foreach (NodeReferenceCollectionAccessor collectionAccessor in accessor.NodeReferenceCollections)
                {
                    IList references = collectionAccessor.Get(node);
                    if (references == null)
                    {
                        continue;
                    }

                    foreach (object item in references)
                    {
                        if (item is INodeReference reference)
                        {
                            sink ^= reference.UUID.GetHashCode();
                        }
                    }
                }

                foreach (VariableAccessor variableAccessor in accessor.Variables)
                {
                    sink ^= variableAccessor.Get(node)?.Type.GetHashCode() ?? 0;
                }

                foreach (VariableCollectionAccessor collectionAccessor in accessor.VariableCollections)
                {
                    IList variables = collectionAccessor.Get(node);
                    if (variables == null)
                    {
                        continue;
                    }

                    foreach (object item in variables)
                    {
                        if (item is VariableBase variable)
                        {
                            sink ^= variable.Type.GetHashCode();
                        }
                    }
                }
            }

            benchmarkSink ^= sink;
        }

        private static TreeNode[] CreateRealAccessorFixtureNodes()
        {
            return new TreeNode[]
            {
                new Sequence
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    events = new[] { new NodeReference(UUID.NewUUID()), new NodeReference(UUID.NewUUID()) },
                },
                new Probability
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    events = new[]
                    {
                        new Probability.EventWeight { weight = 2, reference = new NodeReference(UUID.NewUUID()) },
                        new Probability.EventWeight { weight = 7, reference = new NodeReference(UUID.NewUUID()) },
                    },
                },
                new PseudoProbability
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    maxConsecutiveBranch = CreateVariableField(VariableType.Int),
                    events = new[]
                    {
                        new PseudoProbability.EventWeight { weight = CreateVariableField(VariableType.Int), reference = new NodeReference(UUID.NewUUID()) },
                        new PseudoProbability.EventWeight { weight = CreateVariableField(VariableType.Int), reference = new NodeReference(UUID.NewUUID()) },
                    },
                },
                new GetObjectValue
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    @object = CreateVariableReference(VariableType.UnityObject),
                    type = new GenericTypeReference(),
                    fieldPointers = new List<FieldPointer>
                    {
                        new() { name = "health", data = CreateVariableReference(VariableType.Int) },
                        new() { name = "speed", data = CreateVariableReference(VariableType.Float) },
                    },
                },
                new SetObjectValue
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    @object = CreateVariableReference(VariableType.UnityObject),
                    type = new TypeReference<UnityEngine.Component>(),
                    fieldData = new List<FieldChangeData>
                    {
                        new() { name = "health", data = new Parameter(VariableType.Int) },
                        new() { name = "speed", data = new Parameter(VariableType.Float) },
                    },
                },
            };
        }

        private static NodeAccessor GetGeneratedAccessor(Type type)
        {
            return RealGeneratedAccessorCache.GetOrAdd(type, static nodeType =>
            {
                NodeAccessor accessor = NodeAccessorProvider.GetAccessor(nodeType);
                Assert.That(accessor, Is.InstanceOf<NodePropertyAccessor>());
                return accessor;
            });
        }

        private static NodeAccessor GetFallbackAccessor(Type type)
        {
            return RealFallbackAccessorCache.GetOrAdd(type, static nodeType =>
            {
                if (nodeType == typeof(Sequence)) return NodeAccessor<Sequence>.Create();
                if (nodeType == typeof(Probability)) return NodeAccessor<Probability>.Create();
                if (nodeType == typeof(PseudoProbability)) return NodeAccessor<PseudoProbability>.Create();
                if (nodeType == typeof(GetObjectValue)) return NodeAccessor<GetObjectValue>.Create();
                if (nodeType == typeof(SetObjectValue)) return NodeAccessor<SetObjectValue>.Create();
                throw new NotSupportedException("Missing fallback accessor fixture for " + nodeType.FullName);
            });
        }

        private static VariableField<int> CreateVariableField(VariableType type)
        {
            VariableField<int> field = new();
            field.SetReference(new VariableData(type + " variable", type));
            return field;
        }

        private static VariableReference CreateVariableReference(VariableType type)
        {
            VariableReference reference = new();
            reference.SetReference(new VariableData(type + " variable", type));
            return reference;
        }

        private static IEnumerator CreateRuntimeTree(
            DynamicTreeFixture fixture,
            bool recordSample,
            SampleGroup sampleGroup,
            List<double> samples)
        {
            GameObject gameObject = new("BehaviourTreeInstancePerformanceTests");
            TestBehaviour script = gameObject.AddComponent<TestBehaviour>();

            Stopwatch stopwatch = Stopwatch.StartNew();
            BehaviourTree tree = new(fixture.Data, gameObject, script);
            float timeout = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (!tree.IsInitialized && !tree.IsError && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            stopwatch.Stop();

            UnityEngine.Object.DestroyImmediate(gameObject);

            Assert.That(tree.IsError, Is.False);
            Assert.That(tree.IsInitialized, Is.True);

            if (!recordSample)
            {
                yield break;
            }

            double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
            samples.Add(milliseconds);
            Measure.Custom(sampleGroup, milliseconds);
        }

        private static void WriteSummary(string label, IReadOnlyList<double> generatedSamples, IReadOnlyList<double> fallbackSamples)
        {
            double generatedAverage = generatedSamples.Average();
            double fallbackAverage = fallbackSamples.Average();
            double ratio = fallbackAverage / generatedAverage;

            TestContext.WriteLine(
                $"[Benchmark] {label} ({NodeCount} nodes): " +
                $"generated avg {generatedAverage:F3} ms (best {generatedSamples.Min():F3}, worst {generatedSamples.Max():F3}); " +
                $"fallback avg {fallbackAverage:F3} ms (best {fallbackSamples.Min():F3}, worst {fallbackSamples.Max():F3}); " +
                $"fallback/generated {ratio:F2}x.");
        }

        public static NodeReference[] CloneNodeReferences(NodeReference[] source)
        {
            if (source == null)
            {
                return Array.Empty<NodeReference>();
            }

            NodeReference[] clone = new NodeReference[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i]?.Clone();
            }
            return clone;
        }

        public static NodeReference? CloneNodeReference(NodeReference? source)
        {
            return source?.Clone();
        }

        public static VariableReference? CloneVariableReference(VariableReference? source)
        {
            return (VariableReference?)source?.Clone();
        }

        public static IReadOnlyList<NodeReferenceAccessor> CreateNodeReferenceAccessors(Type nodeType)
        {
            return NodeReferenceAccessorCache.GetOrAdd(nodeType, static _ => new[]
            {
                new NodeReferenceAccessor(
                    "parent",
                    typeof(NodeReference),
                    node => ((TreeNode)node).parent,
                    (node, value) => ((TreeNode)node).parent = (NodeReference)value),
            });
        }

        public static IReadOnlyList<NodeReferenceCollectionAccessor> CreateNodeReferenceCollectionAccessors(Type nodeType)
        {
            return NodeReferenceCollectionAccessorCache.GetOrAdd(nodeType, static type =>
            {
                FieldInfo eventsField = type.GetField("events")!;
                FieldInfo servicesField = typeof(TreeNode).GetField(nameof(TreeNode.services))!;
                return new[]
                {
                    new NodeReferenceCollectionAccessor(
                        eventsField.Name,
                        eventsField.FieldType,
                        typeof(NodeReference),
                        node => (IList)eventsField.GetValue(node),
                        (node, value) => eventsField.SetValue(node, value)),
                    new NodeReferenceCollectionAccessor(
                        servicesField.Name,
                        servicesField.FieldType,
                        typeof(NodeReference),
                        node => (IList)servicesField.GetValue(node),
                        (node, value) => servicesField.SetValue(node, value)),
                };
            });
        }

        public static IReadOnlyList<VariableAccessor> CreateVariableAccessors(Type nodeType)
        {
            return VariableAccessorCache.GetOrAdd(nodeType, static type => new[]
            {
                CreateVariableAccessor(type.GetField("signal")!),
                CreateVariableAccessor(type.GetField("target")!),
            });
        }

        public static IReadOnlyList<VariableCollectionAccessor> GetEmptyVariableCollectionAccessors()
        {
            return Array.Empty<VariableCollectionAccessor>();
        }

        private static VariableAccessor CreateVariableAccessor(FieldInfo field)
        {
            return new VariableAccessor(
                field.Name,
                field.FieldType,
                node => (VariableBase)field.GetValue(node));
        }

        private sealed class DynamicTreeFixture : IDisposable
        {
            private DynamicTreeFixture(Type nodeType, BehaviourTreeData data)
            {
                NodeType = nodeType;
                Data = data;
            }

            public Type NodeType { get; }
            public BehaviourTreeData Data { get; }

            public static DynamicTreeFixture Create(bool includeRegistry, int nodeCount)
            {
                string suffix = Guid.NewGuid().ToString("N");
                AssemblyName assemblyName = new("Aethiumian.AI.Tests.BehaviourTreeInstanceFixture." + suffix);
                AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                ModuleBuilder module = assembly.DefineDynamicModule(assemblyName.Name!);
                Type nodeType = BuildNodeType(module, suffix);

                if (includeRegistry)
                {
                    Type accessorType = BuildAccessorType(module, nodeType, suffix);
                    Type registryType = BuildRegistryType(module, nodeType);
                    NodePropertyAccessor accessor = (NodePropertyAccessor)Activator.CreateInstance(accessorType)!;
                    registryType.GetField("Accessor", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, accessor);
                }

                BehaviourTreeData data = BuildData(nodeType, nodeCount);
                return new DynamicTreeFixture(nodeType, data);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Data);
            }

            private static BehaviourTreeData BuildData(Type nodeType, int nodeCount)
            {
                BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
                data.noActionMaximumDurationLimit = true;

                TreeNode[] nodes = new TreeNode[nodeCount];
                for (int i = 0; i < nodes.Length; i++)
                {
                    TreeNode node = (TreeNode)Activator.CreateInstance(nodeType)!;
                    node.name = "Synthetic Node " + i;
                    node.uuid = UUID.NewUUID();
                    nodeType.GetField("signal")!.SetValue(node, new VariableReference());
                    nodeType.GetField("target")!.SetValue(node, new VariableReference());
                    nodes[i] = node;
                    data.nodes.Add(node);
                }

                data.headNodeUUID = nodes[0].uuid;
                for (int i = 0; i < nodes.Length; i++)
                {
                    List<NodeReference> children = new();
                    int firstChild = (i * 3) + 1;
                    for (int childOffset = 0; childOffset < 3; childOffset++)
                    {
                        int childIndex = firstChild + childOffset;
                        if (childIndex >= nodes.Length)
                        {
                            break;
                        }

                        children.Add(new NodeReference(nodes[childIndex].uuid));
                        nodes[childIndex].parent = new NodeReference(nodes[i].uuid);
                    }

                    nodeType.GetField("events")!.SetValue(nodes[i], children.ToArray());
                }

                return data;
            }

            private static Type BuildNodeType(ModuleBuilder module, string suffix)
            {
                TypeBuilder type = module.DefineType(
                    "Aethiumian.AI.Tests.GeneratedBenchmarkNode_" + suffix,
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(TreeNode));

                type.SetCustomAttribute(new CustomAttributeBuilder(typeof(SerializableAttribute).GetConstructor(Type.EmptyTypes)!, Array.Empty<object>()));
                FieldBuilder eventsField = type.DefineField("events", typeof(NodeReference[]), FieldAttributes.Public);
                FieldBuilder signalField = type.DefineField("signal", typeof(VariableReference), FieldAttributes.Public);
                FieldBuilder targetField = type.DefineField("target", typeof(VariableReference), FieldAttributes.Public);
                DefineNodeConstructor(type, eventsField, signalField, targetField);
                DefineInitialize(type);
                DefineExecute(type);
                return type.CreateType();
            }

            private static Type BuildAccessorType(ModuleBuilder module, Type nodeType, string suffix)
            {
                TypeBuilder type = module.DefineType(
                    "Aethiumian.AI.Accessors.GeneratedBenchmarkNodePropertyAccessor_" + suffix,
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(NodePropertyAccessor));

                DefineDefaultConstructor(type, typeof(NodePropertyAccessor));
                DefineNodeTypeGetter(type, nodeType);
                DefineListGetter<NodeReferenceAccessor>(type, nodeType, nameof(NodeAccessor.NodeReferences), nameof(CreateNodeReferenceAccessors));
                DefineListGetter<NodeReferenceCollectionAccessor>(type, nodeType, nameof(NodeAccessor.NodeReferenceCollections), nameof(CreateNodeReferenceCollectionAccessors));
                DefineListGetter<VariableAccessor>(type, nodeType, nameof(NodeAccessor.Variables), nameof(CreateVariableAccessors));
                DefineParameterlessListGetter<VariableCollectionAccessor>(type, nameof(NodeAccessor.VariableCollections), nameof(GetEmptyVariableCollectionAccessors));
                DefineClone(type, nodeType);
                DefineCopy(type);
                DefineFillNull(type, nodeType);
                return type.CreateType();
            }

            private static Type BuildRegistryType(ModuleBuilder module, Type nodeType)
            {
                TypeBuilder type = module.DefineType(
                    "Aethiumian.AI.Accessors.GeneratedNodePropertyAccessorRegistry",
                    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);

                FieldBuilder accessorField = type.DefineField("Accessor", typeof(NodePropertyAccessor), FieldAttributes.Public | FieldAttributes.Static);
                MethodBuilder method = type.DefineMethod(
                    "TryGet",
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(bool),
                    new[] { typeof(Type), typeof(NodePropertyAccessor).MakeByRefType() });

                ILGenerator il = method.GetILGenerator();
                Label miss = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("op_Equality", new[] { typeof(Type), typeof(Type) })!);
                il.Emit(OpCodes.Brfalse_S, miss);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldsfld, accessorField);
                il.Emit(OpCodes.Stind_Ref);
                il.Emit(OpCodes.Ldc_I4_1);
                il.Emit(OpCodes.Ret);
                il.MarkLabel(miss);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Stind_Ref);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);

                return type.CreateType();
            }

            private static void DefineDefaultConstructor(TypeBuilder type, Type baseType)
            {
                ConstructorBuilder ctor = type.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    Type.EmptyTypes);

                ILGenerator il = ctor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, GetDefaultConstructor(baseType));
                il.Emit(OpCodes.Ret);
            }

            private static void DefineNodeConstructor(
                TypeBuilder type,
                FieldInfo eventsField,
                FieldInfo signalField,
                FieldInfo targetField)
            {
                ConstructorBuilder ctor = type.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    Type.EmptyTypes);

                ILGenerator il = ctor.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, GetDefaultConstructor(typeof(TreeNode)));
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, typeof(Array).GetMethod(nameof(Array.Empty))!.MakeGenericMethod(typeof(NodeReference)));
                il.Emit(OpCodes.Stfld, eventsField);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, typeof(VariableReference).GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Stfld, signalField);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, typeof(VariableReference).GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Stfld, targetField);
                il.Emit(OpCodes.Ret);
            }

            private static ConstructorInfo GetDefaultConstructor(Type type)
            {
                return type.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null)!;
            }

            private static void DefineInitialize(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(TreeNode.Initialize),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    Type.EmptyTypes);

                method.GetILGenerator().Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(TreeNode).GetMethod(nameof(TreeNode.Initialize))!);
            }

            private static void DefineExecute(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(TreeNode.Execute),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(State),
                    Type.EmptyTypes);

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(TreeNode).GetMethod(nameof(TreeNode.Execute))!);
            }

            private static void DefineNodeTypeGetter(TypeBuilder type, Type nodeType)
            {
                MethodBuilder getter = type.DefineMethod(
                    "get_" + nameof(NodeAccessor.NodeType),
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    typeof(Type),
                    Type.EmptyTypes);

                ILGenerator il = getter.GetILGenerator();
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Ret);

                PropertyBuilder property = type.DefineProperty(nameof(NodeAccessor.NodeType), PropertyAttributes.None, typeof(Type), Type.EmptyTypes);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, typeof(NodeAccessor).GetProperty(nameof(NodeAccessor.NodeType))!.GetGetMethod()!);
            }

            private static void DefineListGetter<T>(TypeBuilder type, Type nodeType, string propertyName, string helperName)
            {
                Type propertyType = typeof(IReadOnlyList<T>);
                MethodBuilder getter = type.DefineMethod(
                    "get_" + propertyName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propertyType,
                    Type.EmptyTypes);

                ILGenerator il = getter.GetILGenerator();
                il.Emit(OpCodes.Ldtoken, nodeType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
                il.Emit(OpCodes.Call, typeof(BehaviourTreeInstancePerformanceTests).GetMethod(helperName, BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);

                PropertyBuilder property = type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, typeof(NodeAccessor).GetProperty(propertyName)!.GetGetMethod()!);
            }

            private static void DefineParameterlessListGetter<T>(TypeBuilder type, string propertyName, string helperName)
            {
                Type propertyType = typeof(IReadOnlyList<T>);
                MethodBuilder getter = type.DefineMethod(
                    "get_" + propertyName,
                    MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    propertyType,
                    Type.EmptyTypes);

                ILGenerator il = getter.GetILGenerator();
                il.Emit(OpCodes.Call, typeof(BehaviourTreeInstancePerformanceTests).GetMethod(helperName, BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Ret);

                PropertyBuilder property = type.DefineProperty(propertyName, PropertyAttributes.None, propertyType, Type.EmptyTypes);
                property.SetGetMethod(getter);
                type.DefineMethodOverride(getter, typeof(NodeAccessor).GetProperty(propertyName)!.GetGetMethod()!);
            }

            private static void DefineClone(TypeBuilder type, Type nodeType)
            {
                FieldInfo eventsField = nodeType.GetField("events")!;
                FieldInfo signalField = nodeType.GetField("signal")!;
                FieldInfo targetField = nodeType.GetField("target")!;
                FieldInfo nameField = typeof(TreeNodeBase).GetField(nameof(TreeNodeBase.name))!;
                FieldInfo uuidField = typeof(TreeNodeBase).GetField(nameof(TreeNodeBase.uuid))!;
                FieldInfo parentField = typeof(TreeNodeBase).GetField(nameof(TreeNodeBase.parent))!;

                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.Duplicate),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(TreeNode),
                    new[] { typeof(TreeNode), typeof(DuplicateMode) });

                ILGenerator il = method.GetILGenerator();
                LocalBuilder source = il.DeclareLocal(nodeType);
                LocalBuilder clone = il.DeclareLocal(nodeType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, nodeType);
                il.Emit(OpCodes.Stloc, source);
                il.Emit(OpCodes.Newobj, GetDefaultConstructor(nodeType));
                il.Emit(OpCodes.Stloc, clone);

                EmitCopyField(il, clone, source, nameField);
                EmitCopyField(il, clone, source, uuidField);
                EmitCloneField(il, clone, source, parentField, nameof(CloneNodeReference));
                EmitCloneField(il, clone, source, eventsField, nameof(CloneNodeReferences));
                EmitCloneField(il, clone, source, signalField, nameof(CloneVariableReference));
                EmitCloneField(il, clone, source, targetField, nameof(CloneVariableReference));

                il.Emit(OpCodes.Ldloc, clone);
                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.Duplicate), new[] { typeof(TreeNode), typeof(DuplicateMode) })!);
            }

            private static void DefineCopy(TypeBuilder type)
            {
                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.Copy),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    new[] { typeof(TreeNode), typeof(TreeNode), typeof(DuplicateMode) });

                ILGenerator il = method.GetILGenerator();
                il.Emit(OpCodes.Newobj, typeof(NotSupportedException).GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Throw);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.Copy), new[] { typeof(TreeNode), typeof(TreeNode), typeof(DuplicateMode) })!);
            }

            private static void DefineFillNull(TypeBuilder type, Type nodeType)
            {
                FieldInfo eventsField = nodeType.GetField("events")!;
                FieldInfo signalField = nodeType.GetField("signal")!;
                FieldInfo targetField = nodeType.GetField("target")!;
                MethodBuilder method = type.DefineMethod(
                    nameof(NodePropertyAccessor.FillNull),
                    MethodAttributes.Public | MethodAttributes.Virtual,
                    typeof(void),
                    new[] { typeof(TreeNode) });

                ILGenerator il = method.GetILGenerator();
                LocalBuilder node = il.DeclareLocal(nodeType);
                Label eventsSet = il.DefineLabel();
                Label signalSet = il.DefineLabel();
                Label targetSet = il.DefineLabel();

                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Castclass, nodeType);
                il.Emit(OpCodes.Stloc, node);

                il.Emit(OpCodes.Ldloc, node);
                il.Emit(OpCodes.Ldfld, eventsField);
                il.Emit(OpCodes.Brtrue_S, eventsSet);
                il.Emit(OpCodes.Ldloc, node);
                il.Emit(OpCodes.Call, typeof(Array).GetMethod(nameof(Array.Empty))!.MakeGenericMethod(typeof(NodeReference)));
                il.Emit(OpCodes.Stfld, eventsField);
                il.MarkLabel(eventsSet);

                EmitCreateIfNull(il, node, signalField, typeof(VariableReference), signalSet);
                EmitCreateIfNull(il, node, targetField, typeof(VariableReference), targetSet);

                il.Emit(OpCodes.Ret);
                type.DefineMethodOverride(method, typeof(NodePropertyAccessor).GetMethod(nameof(NodePropertyAccessor.FillNull), new[] { typeof(TreeNode) })!);
            }

            private static void EmitCreateIfNull(ILGenerator il, LocalBuilder node, FieldInfo field, Type fieldType, Label done)
            {
                il.Emit(OpCodes.Ldloc, node);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Brtrue_S, done);
                il.Emit(OpCodes.Ldloc, node);
                il.Emit(OpCodes.Newobj, fieldType.GetConstructor(Type.EmptyTypes)!);
                il.Emit(OpCodes.Stfld, field);
                il.MarkLabel(done);
            }

            private static void EmitCopyField(ILGenerator il, LocalBuilder clone, LocalBuilder source, FieldInfo field)
            {
                il.Emit(OpCodes.Ldloc, clone);
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Stfld, field);
            }

            private static void EmitCloneField(ILGenerator il, LocalBuilder clone, LocalBuilder source, FieldInfo field, string helperName)
            {
                il.Emit(OpCodes.Ldloc, clone);
                il.Emit(OpCodes.Ldloc, source);
                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Call, typeof(BehaviourTreeInstancePerformanceTests).GetMethod(helperName, BindingFlags.Public | BindingFlags.Static)!);
                il.Emit(OpCodes.Stfld, field);
            }
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
        }
    }
}
