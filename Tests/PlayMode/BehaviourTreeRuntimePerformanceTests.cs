#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: Aethiumian.AI.GenerateForAethiumianAI]

namespace Aethiumian.AI.PlayMode.Tests
{
    /// <summary>
    /// Establishes a runtime baseline for a representative, continuously running behaviour tree.
    /// </summary>
    public sealed class BehaviourTreeRuntimePerformanceTests
    {
        private const int WarmupFrames = 10;
        private const int MeasurementFrames = 60;
        private const int SynchronousIterations = 20;
        private const float InitializationTimeoutSeconds = 10f;
        private static readonly int[] PopulationSizes = { 1, 32, 128, 512 };
        private static readonly SampleGroup[] SteadyStateFrameTimes = CreateSampleGroups("BehaviourTree steady-state frame", SampleUnit.Millisecond);
        private static readonly SampleGroup[] SteadyStateFrameAllocations = CreateSampleGroups("BehaviourTree steady-state frame allocation", SampleUnit.Byte);

        private static readonly SampleGroup SynchronousDecisionTime = new(
            "BehaviourTree synchronous decision - representative tree",
            SampleUnit.Millisecond);

        /// <summary>
        /// Measures the current synchronous decision throughput before any scheduler or stack changes.
        /// </summary>
        [UnityTest, Performance]
        public IEnumerator SynchronousDecisionThroughput_Baseline()
        {
            BehaviourTreeData data = CreateSynchronousDecisionData(64);
            BenchmarkHost host = CreateHost("BehaviourTreeSynchronousDecision");
            BehaviourTree tree = new(data, host.gameObject, host);

            try
            {
                yield return WaitUntilInitialized(tree);
                List<double> samples = new(SynchronousIterations);
                int executionCountBefore = BenchmarkNode.TotalExecutions;

                for (int index = 0; index < WarmupFrames; index++)
                {
                    tree.Start();
                }

                for (int index = 0; index < SynchronousIterations; index++)
                {
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    tree.Start();
                    stopwatch.Stop();

                    double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    samples.Add(milliseconds);
                    Measure.Custom(SynchronousDecisionTime, milliseconds);
                    Assert.That(tree.IsRunning, Is.False, "The synchronous representative tree did not complete.");
                }

                Assert.That(BenchmarkNode.TotalExecutions, Is.GreaterThan(executionCountBefore));
                WriteSummary("synchronous decision", "1 tree", samples, 1, BenchmarkNode.TotalExecutions - executionCountBefore);
            }
            finally
            {
                EndAndDestroy(tree, data, host);
            }
        }

        /// <summary>
        /// Measures steady-state cost and managed allocation as the number of active trees grows.
        /// </summary>
        [UnityTest, Performance]
        public IEnumerator PersistentRepresentativeTrees_ScaleBaseline()
        {
            foreach (int populationSize in PopulationSizes)
            {
                BehaviourTreeData data = CreateRepresentativeData();
                List<BehaviourTree> trees = new(populationSize);
                List<BenchmarkHost> hosts = new(populationSize);

                try
                {
                    for (int index = 0; index < populationSize; index++)
                    {
                        BenchmarkHost host = CreateHost($"BehaviourTreeRuntimePerformance_{populationSize}_{index}");
                        hosts.Add(host);
                        BehaviourTree tree = new(data, host.gameObject, host);
                        trees.Add(tree);
                    }

                    yield return WaitUntilInitialized(trees);
                    StartTrees(trees);
                    yield return null;
                    TickTrees(trees);

                    for (int frame = 0; frame < WarmupFrames; frame++)
                    {
                        yield return null;
                        TickTrees(trees);
                    }

                    int executionCountBefore = BenchmarkNode.TotalExecutions;
                    List<double> frameSamples = new(MeasurementFrames);
                    List<double> allocationSamples = new(MeasurementFrames);
                    int maxActiveStackCount = 0;

                    for (int frame = 0; frame < MeasurementFrames; frame++)
                    {
                        yield return null;

                        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        TickTrees(trees);
                        stopwatch.Stop();
                        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                        double milliseconds = stopwatch.Elapsed.TotalMilliseconds;
                        frameSamples.Add(milliseconds);
                        allocationSamples.Add(allocatedBytes);
                        maxActiveStackCount = Math.Max(maxActiveStackCount, CountActiveStacks(trees));
                        Measure.Custom(SteadyStateFrameTimes[PopulationIndex(populationSize)], milliseconds);
                        Measure.Custom(SteadyStateFrameAllocations[PopulationIndex(populationSize)], allocatedBytes);
                    }

                    int executionCount = BenchmarkNode.TotalExecutions - executionCountBefore;
                    Assert.That(executionCount, Is.GreaterThan(0), $"No benchmark nodes executed for population {populationSize}.");
                    for (int index = 0; index < trees.Count; index++)
                    {
                        Assert.That(trees[index].IsError, Is.False, $"Benchmark tree {index} entered an error state.");
                        Assert.That(trees[index].IsRunning, Is.True, $"Benchmark tree {index} stopped unexpectedly.");
                    }

                    WriteSummary(
                        "steady state",
                        $"{populationSize} trees",
                        frameSamples,
                        populationSize,
                        executionCount,
                        allocationSamples,
                        maxActiveStackCount);
                }
                finally
                {
                    for (int index = 0; index < trees.Count; index++)
                    {
                        EndAndDestroy(trees[index], data, hosts[index], destroyData: false);
                    }

                    for (int index = trees.Count; index < hosts.Count; index++)
                    {
                        UnityEngine.Object.DestroyImmediate(hosts[index].gameObject);
                    }

                    UnityEngine.Object.DestroyImmediate(data);
                }
            }
        }

        /// <summary>
        /// Creates a representative finite decision tree used only for the synchronous baseline.
        /// </summary>
        private static BehaviourTreeData CreateSynchronousDecisionData(int childCount)
        {
            BehaviourTreeData data = CreateData();
            Sequence root = CreateNode<Sequence>("Synchronous Decision Root");
            data.headNodeUUID = root.uuid;
            data.nodes.Add(root);

            NodeReference[] children = new NodeReference[childCount];
            for (int index = 0; index < childCount; index++)
            {
                BenchmarkNode child = CreateNode<BenchmarkNode>($"Synchronous Work {index}");
                child.parent = new NodeReference(root.uuid);
                children[index] = new NodeReference(child.uuid);
                data.nodes.Add(child);
            }

            root.events = children;
            return data;
        }

        /// <summary>
        /// Creates a continuously running tree with branching, conditions, variables, services, and parallel stacks.
        /// </summary>
        private static BehaviourTreeData CreateRepresentativeData()
        {
            BehaviourTreeData data = CreateData();
            VariableData decisionVariable = new("DecisionInput", VariableType.Int);
            decisionVariable.SetDefaultValue(1);
            data.variables.Add(decisionVariable);

            Parallel root = CreateNode<Parallel>("Combat Coordinator");
            root.mode = Parallel.Mode.WaitAll;
            data.headNodeUUID = root.uuid;
            data.nodes.Add(root);

            Sequence decisionBranch = CreateNode<Sequence>("Target Selection Branch");
            Decision decision = CreateNode<Decision>("Target Decision");
            BenchmarkCondition noTarget = CreateNode<BenchmarkCondition>("No Target");
            noTarget.result = false;
            BenchmarkCondition targetFound = CreateNode<BenchmarkCondition>("Target Found");
            targetFound.result = true;
            BenchmarkCondition fallbackTarget = CreateNode<BenchmarkCondition>("Fallback Target");
            fallbackTarget.result = true;
            LoopFlow decisionLoop = CreateNode<LoopFlow>("Target Tracking");
            AddChildren(decision, noTarget, targetFound, fallbackTarget);
            AddChildren(decisionBranch, decision, decisionLoop);
            TimedBenchmarkService targetScanService = CreateNode<TimedBenchmarkService>("Target Scan Service");
            targetScanService.interval = 8;
            decisionBranch.AddService(targetScanService);

            Sequence actionBranch = CreateNode<Sequence>("Combat Action Branch");
            VariableProbe variableProbe = CreateNode<VariableProbe>("Read Decision Variable");
            variableProbe.variableUUID = decisionVariable.UUID;
            InlineBenchmarkAction action = CreateNode<InlineBenchmarkAction>("Execute Attack Intent");
            LoopFlow actionLoop = CreateNode<LoopFlow>("Attack Recovery");
            AddChildren(actionBranch, variableProbe, action, actionLoop);
            TimedBenchmarkService combatTimerService = CreateNode<TimedBenchmarkService>("Combat Timer Service");
            combatTimerService.interval = 8;
            actionBranch.AddService(combatTimerService);

            Sequence supportBranch = CreateNode<Sequence>("Support Evaluation Branch");
            List<TreeNode> supportChildren = new();
            for (int index = 0; index < 36; index++)
            {
                supportChildren.Add(CreateNode<BenchmarkNode>($"Support Check {index}"));
            }

            Decision supportDecision = CreateNode<Decision>("Support Decision");
            BenchmarkCondition supportUnavailable = CreateNode<BenchmarkCondition>("Support Unavailable");
            supportUnavailable.result = false;
            BenchmarkCondition supportAvailable = CreateNode<BenchmarkCondition>("Support Available");
            supportAvailable.result = true;
            AddChildren(supportDecision, supportUnavailable, supportAvailable);
            supportChildren.Insert(0, supportDecision);

            LoopFlow supportLoop = CreateNode<LoopFlow>("Support Loop");
            supportChildren.Add(supportLoop);
            AddChildren(supportBranch, supportChildren.ToArray());
            TimedBenchmarkService supportTimerService = CreateNode<TimedBenchmarkService>("Support Timer Service");
            supportTimerService.interval = 8;
            supportBranch.AddService(supportTimerService);

            AddChildren(root, decisionBranch, actionBranch, supportBranch);
            AddNodes(data, decisionBranch, decision, noTarget, targetFound, fallbackTarget, decisionLoop, targetScanService);
            AddNodes(data, actionBranch, variableProbe, action, actionLoop, combatTimerService);
            AddNodes(data, supportBranch);
            AddNodes(data, supportTimerService);
            AddNodes(data, supportDecision, supportUnavailable, supportAvailable);
            foreach (TreeNode child in supportChildren)
            {
                if (!data.nodes.Contains(child))
                {
                    data.nodes.Add(child);
                }
            }

            return data;
        }

        /// <summary>
        /// Creates common data settings shared by all benchmark trees.
        /// </summary>
        private static BehaviourTreeData CreateData()
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.nodeErrorHandle = NodeErrorSolution.Throw;
            return data;
        }

        /// <summary>
        /// Creates one stable Performance Testing sample group for each population size.
        /// </summary>
        private static SampleGroup[] CreateSampleGroups(string prefix, SampleUnit unit)
        {
            SampleGroup[] groups = new SampleGroup[PopulationSizes.Length];
            for (int index = 0; index < PopulationSizes.Length; index++)
            {
                groups[index] = new SampleGroup($"{prefix} - {PopulationSizes[index]} trees", unit);
            }

            return groups;
        }

        /// <summary>
        /// Resolves a population size to its pre-created sample group index.
        /// </summary>
        private static int PopulationIndex(int populationSize)
        {
            for (int index = 0; index < PopulationSizes.Length; index++)
            {
                if (PopulationSizes[index] == populationSize)
                {
                    return index;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(populationSize));
        }

        /// <summary>
        /// Adds child references and parent links for a flow node.
        /// </summary>
        private static void AddChildren(Flow parent, params TreeNode[] children)
        {
            NodeReference[] references = new NodeReference[children.Length];
            for (int index = 0; index < children.Length; index++)
            {
                children[index].parent = new NodeReference(parent.uuid);
                references[index] = new NodeReference(children[index].uuid);
            }

            switch (parent)
            {
                case Sequence sequence:
                    sequence.events = references;
                    break;
                case Decision decision:
                    decision.events = references;
                    break;
                case Parallel parallel:
                    parallel.events = references;
                    break;
                default:
                    throw new ArgumentException($"Unsupported benchmark flow type {parent.GetType().Name}.", nameof(parent));
            }
        }

        /// <summary>
        /// Adds the provided prototype nodes to benchmark data once.
        /// </summary>
        private static void AddNodes(BehaviourTreeData data, params TreeNode[] nodes)
        {
            foreach (TreeNode node in nodes)
            {
                if (!data.nodes.Contains(node))
                {
                    data.nodes.Add(node);
                }
            }
        }

        /// <summary>
        /// Creates a fresh prototype node with a stable name and UUID.
        /// </summary>
        private static T CreateNode<T>(string name) where T : TreeNode, new()
        {
            return new T
            {
                name = name,
                uuid = UUID.NewUUID(),
                parent = NodeReference.Empty,
            };
        }

        /// <summary>
        /// Creates the host component required by a runtime BehaviourTree.
        /// </summary>
        private static BenchmarkHost CreateHost(string name)
        {
            GameObject gameObject = new(name);
            return gameObject.AddComponent<BenchmarkHost>();
        }

        /// <summary>
        /// Waits for one tree to finish its asynchronous initialization.
        /// </summary>
        private static IEnumerator WaitUntilInitialized(BehaviourTree tree)
        {
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (!tree.IsInitialized && !tree.IsError && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(tree.IsError, Is.False, "Benchmark tree initialization failed.");
            Assert.That(tree.IsInitialized, Is.True, "Benchmark tree did not initialize.");
        }

        /// <summary>
        /// Waits for every tree in a population to finish initialization.
        /// </summary>
        private static IEnumerator WaitUntilInitialized(IReadOnlyList<BehaviourTree> trees)
        {
            float deadline = Time.realtimeSinceStartup + InitializationTimeoutSeconds;
            while (true)
            {
                bool initialized = true;
                for (int index = 0; index < trees.Count; index++)
                {
                    if (trees[index].IsError)
                    {
                        Assert.Fail($"Benchmark tree {index} failed during initialization.");
                    }

                    initialized &= trees[index].IsInitialized;
                }

                if (initialized || Time.realtimeSinceStartup >= deadline)
                {
                    Assert.That(initialized, Is.True, "Benchmark trees did not initialize before the timeout.");
                    yield break;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Starts every benchmark tree after initialization has completed.
        /// </summary>
        private static void StartTrees(IReadOnlyList<BehaviourTree> trees)
        {
            for (int index = 0; index < trees.Count; index++)
            {
                trees[index].Start();
            }
        }

        /// <summary>
        /// Drives the same Update, LateUpdate, and FixedUpdate order used by AI.
        /// </summary>
        private static void TickTrees(IReadOnlyList<BehaviourTree> trees)
        {
            for (int index = 0; index < trees.Count; index++)
            {
                BehaviourTree tree = trees[index];
                tree.Update();
                tree.LateUpdate();
                tree.FixedUpdate();
            }
        }

        /// <summary>
        /// Counts all currently registered stacks, including parallel and service stacks.
        /// </summary>
        private static int CountActiveStacks(IReadOnlyList<BehaviourTree> trees)
        {
            int count = 0;
            for (int index = 0; index < trees.Count; index++)
            {
                count += trees[index].ActiveStacks.Count;
            }

            return count;
        }

        /// <summary>
        /// Ends a tree and destroys its temporary Unity objects.
        /// </summary>
        private static void EndAndDestroy(BehaviourTree tree, BehaviourTreeData data, BenchmarkHost host, bool destroyData = true)
        {
            if (tree.IsRunning)
            {
                tree.End();
            }

            UnityEngine.Object.DestroyImmediate(host.gameObject);
            if (destroyData)
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }

        /// <summary>
        /// Writes a compact baseline summary while preserving raw Performance Testing samples.
        /// </summary>
        private static void WriteSummary(
            string label,
            string population,
            IReadOnlyList<double> samples,
            int treeCount,
            int executionCount,
            IReadOnlyList<double>? allocations = null,
            int activeStackCount = 0)
        {
            List<double> ordered = new(samples);
            ordered.Sort();
            double average = 0;
            for (int index = 0; index < samples.Count; index++)
            {
                average += samples[index];
            }

            average /= samples.Count;
            double p50 = ordered[(ordered.Count - 1) / 2];
            double p95 = ordered[Math.Min(ordered.Count - 1, (int)Math.Ceiling(ordered.Count * 0.95) - 1)];
            double totalMilliseconds = average * samples.Count;
            double executionsPerSecond = totalMilliseconds > 0 ? executionCount * 1000d / totalMilliseconds : 0;
            double allocationAverage = 0;
            if (allocations != null)
            {
                for (int index = 0; index < allocations.Count; index++)
                {
                    allocationAverage += allocations[index];
                }

                allocationAverage /= allocations.Count;
            }

            TestContext.WriteLine(
                $"[BehaviourTreeBaseline] {label}, population={population}, trees={treeCount}, " +
                $"avg={average:F4} ms, p50={p50:F4} ms, p95={p95:F4} ms, max={ordered[^1]:F4} ms, " +
                $"avgAlloc={allocationAverage:F1} bytes/frame, maxActiveStacks={activeStackCount}, " +
                $"executions={executionCount}, executionsPerSecond={executionsPerSecond:F1}.");
        }

        /// <summary>
        /// Minimal MonoBehaviour host used by benchmark BehaviourTree instances.
        /// </summary>
        private sealed class BenchmarkHost : MonoBehaviour
        {
        }

        /// <summary>
        /// A deterministic successful node that records execution volume.
        /// </summary>
        [Serializable]
        public class BenchmarkNode : TreeNode
        {
            public static int TotalExecutions { get; private set; }

            /// <summary>
            /// Records one deterministic benchmark node execution.
            /// </summary>
            public static void RecordExecution()
            {
                TotalExecutions++;
            }

            /// <summary>
            /// Initializes the deterministic benchmark node state.
            /// </summary>
            public override void Initialize()
            {
            }

            /// <summary>
            /// Completes one synchronous benchmark node execution.
            /// </summary>
            public override State Execute()
            {
                RecordExecution();
                return State.Success;
            }
        }

        /// <summary>
        /// A deterministic condition node used to exercise failed and successful decision branches.
        /// </summary>
        [Serializable]
        public sealed class BenchmarkCondition : BenchmarkNode
        {
            public bool result;

            /// <summary>
            /// Returns the authored deterministic condition result.
            /// </summary>
            public override State Execute()
            {
                RecordExecution();
                return result ? State.Success : State.Failed;
            }
        }

        /// <summary>
        /// Reads a runtime variable and returns its deterministic branch result.
        /// </summary>
        [Serializable]
        public sealed class VariableProbe : BenchmarkNode
        {
            public UUID variableUUID;

            /// <summary>
            /// Reads the benchmark variable and returns its deterministic branch result.
            /// </summary>
            public override State Execute()
            {
                RecordExecution();
                return behaviourTree.GetVariable(variableUUID).IntValue > 0 ? State.Success : State.Failed;
            }
        }

        /// <summary>
        /// Keeps a branch alive by yielding once per game frame.
        /// </summary>
        [Serializable]
        public sealed class LoopFlow : Flow
        {
            /// <summary>
            /// Initializes the deterministic looping flow state.
            /// </summary>
            public override void Initialize()
            {
            }

            /// <summary>
            /// Yields so the branch remains active across frames.
            /// </summary>
            public override State Execute()
            {
                BenchmarkNode.RecordExecution();
                return State.Yield;
            }
        }

        /// <summary>
        /// Completes inline to represent an action boundary without external game systems.
        /// </summary>
        [Serializable]
        public sealed class InlineBenchmarkAction : Aethiumian.AI.Nodes.Action
        {
            /// <summary>
            /// Completes the inline benchmark action immediately.
            /// </summary>
            public override void Start()
            {
                Success();
            }
        }

        /// <summary>
        /// Runs a small deterministic service stack at a fixed frame interval.
        /// </summary>
        [Serializable]
        public sealed class TimedBenchmarkService : RepeatService
        {
            /// <summary>
            /// Initializes the deterministic benchmark service state.
            /// </summary>
            public override void Initialize()
            {
            }

            /// <summary>
            /// Records a timed service execution and resets its interval.
            /// </summary>
            public override State Execute()
            {
                BenchmarkNode.RecordExecution();
                ResetTimer();
                return State.Success;
            }
        }
    }
}
