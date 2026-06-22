#nullable enable
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using DeepCloneUtility = Aethiumian.AI.Utils.DeepClone;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        private static readonly SampleGroup ProductionGeneratedInstanceTime = new(
            "BehaviourTree instance creation - production generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup GeneratedCloneTime = new(
            "TreeNode clone only - production generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup FallbackCloneTime = new(
            "TreeNode clone only - reflection fallback clone",
            SampleUnit.Millisecond);

        private static readonly SampleGroup GeneratedAccessorEnumerationTime = new(
            "TreeNode accessor enumeration - production generated accessors",
            SampleUnit.Millisecond);

        private static readonly SampleGroup FallbackAccessorEnumerationTime = new(
            "TreeNode accessor enumeration - reflection fallback accessors",
            SampleUnit.Millisecond);

        private static readonly ConcurrentDictionary<Type, NodeAccessor> RealFallbackAccessorCache = new();
        private static readonly ConcurrentDictionary<Type, NodeAccessor> RealGeneratedAccessorCache = new();
        private static int benchmarkSink;

        [UnityTest, Performance]
        public IEnumerator CreateBehaviourTreeInstance_ProductionGeneratedAccessors()
        {
            using RealTreeFixture fixture = RealTreeFixture.CreateMixedRuntimeTree(NodeCount);
            List<double> generatedSamples = new();

            AssertGeneratedAccessorsFor(fixture.Data.nodes);

            for (int i = 0; i < WarmupCount; i++)
            {
                yield return CreateRuntimeTree(fixture, recordSample: false, ProductionGeneratedInstanceTime, generatedSamples);
            }

            for (int i = 0; i < MeasurementCount; i++)
            {
                yield return CreateRuntimeTree(fixture, recordSample: true, ProductionGeneratedInstanceTime, generatedSamples);
            }

            WriteSingleSummary("BehaviourTree instance creation with production generated accessors", generatedSamples);
        }

        [Test, Performance]
        public void CloneNodes_ProductionGeneratedAccessorsVsFallbackClone()
        {
            TreeNode[] nodes = CreateRealAccessorFixtureNodes(NodeCount);
            List<double> generatedSamples = new();
            List<double> fallbackSamples = new();

            AssertGeneratedAccessorsFor(nodes);

            Warmup(() => CloneAllNodes(nodes, NodeFactory.Duplicate));
            Warmup(() => CloneAllNodes(nodes, DeepCloneUtility.Clone));

            for (int i = 0; i < MeasurementCount; i++)
            {
                MeasureAction(generatedSamples, GeneratedCloneTime, () => CloneAllNodes(nodes, NodeFactory.Duplicate));
                MeasureAction(fallbackSamples, FallbackCloneTime, () => CloneAllNodes(nodes, DeepCloneUtility.Clone));
            }

            WriteComparisonSummary("TreeNode clone only", generatedSamples, fallbackSamples);
        }

        [Test, Performance]
        public void EnumerateAccessors_ProductionGeneratedAccessorsVsFallbackAccessors()
        {
            TreeNode[] nodes = CreateRealAccessorFixtureNodes(NodeCount);
            List<double> generatedSamples = new();
            List<double> fallbackSamples = new();

            AssertGeneratedAccessorsFor(nodes);

            Warmup(() => EnumerateAccessorFields(nodes, GetGeneratedAccessor));
            Warmup(() => EnumerateAccessorFields(nodes, GetFallbackAccessor));

            for (int i = 0; i < MeasurementCount; i++)
            {
                MeasureAction(generatedSamples, GeneratedAccessorEnumerationTime, () => EnumerateAccessorFields(nodes, GetGeneratedAccessor));
                MeasureAction(fallbackSamples, FallbackAccessorEnumerationTime, () => EnumerateAccessorFields(nodes, GetFallbackAccessor));
            }

            WriteComparisonSummary("TreeNode accessor enumeration on real nodes", generatedSamples, fallbackSamples);
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

        private static void CloneAllNodes(TreeNode[] nodes, Func<TreeNode, TreeNode> cloneNode)
        {
            int sink = 0;
            foreach (TreeNode node in nodes)
            {
                TreeNode clone = cloneNode(node);
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
                foreach (INodeReferenceFieldAccessor nodeReferenceAccessor in accessor.NodeReferences)
                {
                    INodeReference reference = nodeReferenceAccessor.Get(node);
                    sink ^= reference?.UUID.GetHashCode() ?? 0;
                }

                foreach (INodeReferenceCollectionFieldAccessor collectionAccessor in accessor.NodeReferenceCollections)
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

                foreach (IVariableFieldAccessor variableAccessor in accessor.Variables)
                {
                    sink ^= variableAccessor.Get(node)?.Type.GetHashCode() ?? 0;
                }

                foreach (IVariableCollectionFieldAccessor collectionAccessor in accessor.VariableCollections)
                {
                    IList variables = collectionAccessor.Get(node);
                    if (variables == null)
                    {
                        continue;
                    }

                    foreach (object item in variables)
                    {
                        if (item is IVariableField variable)
                        {
                            sink ^= variable.Type.GetHashCode();
                        }
                    }
                }
            }

            benchmarkSink ^= sink;
        }

        private static TreeNode[] CreateRealAccessorFixtureNodes(int nodeCount)
        {
            TreeNode[] nodes = new TreeNode[nodeCount];
            for (int i = 0; i < nodes.Length; i++)
            {
                TreeNode node = CreateRealAccessorFixtureNode(i);
                node.name = "Real SourceGen Benchmark Node " + i;
                node.uuid = UUID.NewUUID();
                nodes[i] = node;
            }

            return nodes;
        }

        private static TreeNode CreateRealAccessorFixtureNode(int index)
        {
            return (index % 5) switch
            {
                0 => new Sequence
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    events = new[] { new NodeReference(UUID.NewUUID()), new NodeReference(UUID.NewUUID()) },
                },
                1 => new Probability
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    events = new[]
                    {
                        new Probability.EventWeight { weight = 2, reference = new NodeReference(UUID.NewUUID()) },
                        new Probability.EventWeight { weight = 7, reference = new NodeReference(UUID.NewUUID()) },
                    },
                },
                2 => new PseudoProbability
                {
                    parent = new NodeReference(UUID.NewUUID()),
                    maxConsecutiveBranch = CreateVariableField(VariableType.Int),
                    events = new[]
                    {
                        new PseudoProbability.EventWeight { weight = CreateVariableField(VariableType.Int), reference = new NodeReference(UUID.NewUUID()) },
                        new PseudoProbability.EventWeight { weight = CreateVariableField(VariableType.Int), reference = new NodeReference(UUID.NewUUID()) },
                    },
                },
                3 => new GetObjectValue
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
                _ => new SetObjectValue
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
                Assert.That(
                    accessor,
                    Is.InstanceOf<NodePropertyAccessor>(),
                    "NodeAccessorProvider returned a fallback accessor for " + nodeType.FullName + ". Source generation is not participating in this benchmark.");
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

        private static void AssertGeneratedAccessorsFor(IEnumerable<TreeNode> nodes)
        {
            foreach (Type nodeType in nodes.Select(static node => node.GetType()).Distinct())
            {
                // Test-defined node types are compiled after source generation, so they cannot prove real generator participation.
                Assert.That(
                    GeneratedNodePropertyAccessorProvider.TryGet(nodeType, out _),
                    Is.True,
                    "Missing real source-generated NodePropertyAccessor for " + nodeType.FullName + ". Test-defined node types cannot validate source generator participation.");
            }
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

        private static VariableReference CreateVariableReference(VariableData variable)
        {
            VariableReference reference = new();
            reference.SetReference(variable);
            return reference;
        }

        private static VariableReference<T> CreateTypedVariableReference<T>(VariableData variable)
        {
            VariableReference<T> reference = new();
            reference.SetReference(variable);
            return reference;
        }

        private static VariableField<T> CreateVariableField<T>(VariableData variable)
        {
            VariableField<T> field = new();
            field.SetReference(variable);
            return field;
        }

        private static Parameter CreateParameter(VariableData variable, Type parameterType)
        {
            Parameter parameter = new(parameterType);
            parameter.SetReference(variable);
            return parameter;
        }

        private static VariableData CreateVariable(string name, VariableType type, string defaultValue)
        {
            return new VariableData(name, type)
            {
                DefaultValue = defaultValue,
            };
        }

        private static VariableData CreateUnityObjectVariable(string name, Type objectType)
        {
            VariableData variable = CreateVariable(name, VariableType.UnityObject, string.Empty);
            variable.SetBaseType(typeof(UnityEngine.Object));
            variable.TypeReference.SetReferType(objectType);
            return variable;
        }

        private static IEnumerator CreateRuntimeTree(
            RealTreeFixture fixture,
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

        private static void WriteSingleSummary(string label, IReadOnlyList<double> generatedSamples)
        {
            TestContext.WriteLine(
                $"[Benchmark] {label} ({NodeCount} nodes): " +
                $"generated avg {generatedSamples.Average():F3} ms (best {generatedSamples.Min():F3}, worst {generatedSamples.Max():F3}).");
        }

        private static void WriteComparisonSummary(string label, IReadOnlyList<double> generatedSamples, IReadOnlyList<double> fallbackSamples)
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

        private sealed class RealTreeFixture : IDisposable
        {
            private RealTreeFixture(BehaviourTreeData data)
            {
                Data = data;
            }

            public BehaviourTreeData Data { get; }

            public static RealTreeFixture CreateMixedRuntimeTree(int nodeCount)
            {
                BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
                data.noActionMaximumDurationLimit = true;
                BenchmarkVariables variables = BenchmarkVariables.Create();
                data.variables.AddRange(variables.All);

                List<TreeNode> mainNodes = new(nodeCount);
                Sequence head = CreateNode<Sequence>("Real SourceGen Runtime Head");
                mainNodes.Add(head);
                data.nodes.Add(head);

                for (int i = 1; data.nodes.Count < nodeCount; i++)
                {
                    TreeNode node = CreateRuntimeNode(i, variables);
                    mainNodes.Add(node);
                    data.nodes.Add(node);

                    if (node is ServiceHostNode host && i % 10 == 0)
                    {
                        AddServices(data, host, variables, mainNodes, nodeCount);
                    }
                }

                data.headNodeUUID = head.uuid;
                LinkMainTree(mainNodes);

                return new RealTreeFixture(data);
            }

            private static T CreateNode<T>(string name) where T : TreeNode, new()
            {
                return new T
                {
                    name = name,
                    uuid = UUID.NewUUID(),
                    parent = NodeReference.Empty,
                };
            }

            private static TreeNode CreateRuntimeNode(int index, BenchmarkVariables variables)
            {
                TreeNode node = (index % 7) switch
                {
                    0 => new Sequence(),
                    1 => new Probability
                    {
                        events = new[]
                        {
                            new Probability.EventWeight { weight = 3, reference = NodeReference.Empty },
                            new Probability.EventWeight { weight = 5, reference = NodeReference.Empty },
                        },
                    },
                    2 => new PseudoProbability
                    {
                        maxConsecutiveBranch = CreateVariableField<int>(variables.MaxBranch),
                        events = new[]
                        {
                            new PseudoProbability.EventWeight { weight = CreateVariableField<int>(variables.Weight), reference = NodeReference.Empty },
                            new PseudoProbability.EventWeight { weight = CreateVariableField<int>(variables.StaticWeight), reference = NodeReference.Empty },
                        },
                    },
                    3 => new FunctionCall
                    {
                        function = CreateFunctionReference(),
                        targetObject = CreateTargetScriptReceiver(),
                        parameters = new List<Parameter>
                        {
                            CreateParameter(variables.Weight, typeof(int)),
                            CreateParameter(variables.Label, typeof(string)),
                        },
                        result = CreateVariableReference(variables.Result),
                    },
                    4 => new GetObjectValue
                    {
                        @object = CreateVariableReference(variables.UnityObject),
                        type = new GenericTypeReference(),
                        fieldPointers = new List<FieldPointer>
                        {
                            new() { name = nameof(Transform.position), data = CreateVariableReference(variables.Position) },
                            new() { name = nameof(Transform.name), data = CreateVariableReference(variables.Label) },
                        },
                    },
                    5 => new SetObjectValue
                    {
                        @object = CreateVariableReference(variables.UnityObject),
                        type = new TypeReference<UnityEngine.Component>(),
                        fieldData = new List<FieldChangeData>
                        {
                            new() { name = nameof(Transform.name), data = CreateParameter(variables.Label, typeof(string)) },
                        },
                    },
                    _ => new Nodes.Boolean
                    {
                        boolean = CreateVariableReference(variables.Condition),
                    },
                };

                node.name = "Real SourceGen Runtime Node " + index;
                node.uuid = UUID.NewUUID();
                node.parent = NodeReference.Empty;
                return node;
            }

            private static FunctionReference CreateFunctionReference()
            {
                FunctionReference reference = new();
                reference.SetMethod(typeof(TestBehaviour).GetMethod(nameof(TestBehaviour.BenchmarkFunction)));
                return reference;
            }

            private static VariableReference CreateTargetScriptReceiver()
            {
                VariableReference receiver = new();
                receiver.SetReference(VariableData.GetTargetScriptVariable(typeof(TestBehaviour)));
                return receiver;
            }

            private static void AddServices(
                BehaviourTreeData data,
                ServiceHostNode host,
                BenchmarkVariables variables,
                IReadOnlyList<TreeNode> mainNodes,
                int nodeCount)
            {
                if (data.nodes.Count < nodeCount)
                {
                    Update update = CreateNode<Update>("Real SourceGen Runtime Update Service " + data.nodes.Count);
                    update.forceStopped = CreateVariableField<bool>(variables.Condition);
                    update.subtreeHead = new NodeReference(mainNodes[0].uuid);
                    host.AddService(update);
                    data.nodes.Add(update);
                }

                if (data.nodes.Count < nodeCount)
                {
                    Timer timer = CreateNode<Timer>("Real SourceGen Runtime Timer Service " + data.nodes.Count);
                    timer.updatingVariable = CreateTypedVariableReference<float>(variables.Timer);
                    timer.timing = Timer.Timing.FixedDeltaTime;
                    host.AddService(timer);
                    data.nodes.Add(timer);
                }
            }

            private static void LinkMainTree(IReadOnlyList<TreeNode> nodes)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is not Sequence sequence)
                    {
                        continue;
                    }

                    List<NodeReference> children = new();
                    int firstChild = (i * 3) + 1;
                    for (int childOffset = 0; childOffset < 3; childOffset++)
                    {
                        int childIndex = firstChild + childOffset;
                        if (childIndex >= nodes.Count)
                        {
                            break;
                        }

                        children.Add(new NodeReference(nodes[childIndex].uuid));
                        nodes[childIndex].parent = new NodeReference(sequence.uuid);
                    }

                    sequence.events = children.ToArray();
                }
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Data);
            }
        }

        private sealed class BenchmarkVariables
        {
            private BenchmarkVariables(
                VariableData weight,
                VariableData staticWeight,
                VariableData maxBranch,
                VariableData timer,
                VariableData condition,
                VariableData result,
                VariableData label,
                VariableData position,
                VariableData unityObject)
            {
                Weight = weight;
                StaticWeight = staticWeight;
                MaxBranch = maxBranch;
                Timer = timer;
                Condition = condition;
                Result = result;
                Label = label;
                Position = position;
                UnityObject = unityObject;
                All = new[]
                {
                    Weight,
                    StaticWeight,
                    MaxBranch,
                    Timer,
                    Condition,
                    Result,
                    Label,
                    Position,
                    UnityObject,
                };
            }

            public VariableData Weight { get; }
            public VariableData StaticWeight { get; }
            public VariableData MaxBranch { get; }
            public VariableData Timer { get; }
            public VariableData Condition { get; }
            public VariableData Result { get; }
            public VariableData Label { get; }
            public VariableData Position { get; }
            public VariableData UnityObject { get; }
            public IReadOnlyList<VariableData> All { get; }

            public static BenchmarkVariables Create()
            {
                VariableData staticWeight = CreateVariable("Static Weight", VariableType.Int, "2");
                staticWeight.IsStatic = true;

                return new BenchmarkVariables(
                    CreateVariable("Weight", VariableType.Int, "5"),
                    staticWeight,
                    CreateVariable("Max Branch", VariableType.Int, "3"),
                    CreateVariable("Timer", VariableType.Float, "1"),
                    CreateVariable("Condition", VariableType.Bool, "false"),
                    CreateVariable("Result", VariableType.Bool, "false"),
                    CreateVariable("Label", VariableType.String, "benchmark"),
                    CreateVariable("Position", VariableType.Vector3, "0,0,0"),
                    CreateUnityObjectVariable("External Object", typeof(Transform)));
            }
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
            public bool BenchmarkFunction(int value, string label)
            {
                return value > 0 && !string.IsNullOrEmpty(label);
            }
        }
    }
}
