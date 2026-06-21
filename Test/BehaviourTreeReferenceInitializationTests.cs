#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using WaitUntil = Aethiumian.AI.Nodes.WaitUntil;

namespace Aethiumian.AI.Tests
{
    public sealed class BehaviourTreeReferenceInitializationTests
    {
        [UnityTest]
        public IEnumerator LinkReference_DoesNotEagerBindNodeReferences()
        {
            LinkProbeNode target = CreateNode<LinkProbeNode>("target");
            LinkProbeNode rawTarget = CreateNode<LinkProbeNode>("raw-target");
            LinkProbeNode head = CreateNode<LinkProbeNode>("head");
            head.direct = new NodeReference(target.uuid);
            head.children = new[] { new NodeReference(target.uuid) };
            head.raw = new RawNodeReference { UUID = rawTarget.uuid };
            head.weighted = new[]
            {
                new Probability.EventWeight { reference = new NodeReference(target.uuid) },
            };
            head.pseudoWeighted = new[]
            {
                new PseudoProbability.EventWeight { reference = new NodeReference(target.uuid) },
            };

            using TreeFixture fixture = CreateFixture(head, target, rawTarget);
            yield return fixture.WaitUntilReady();

            LinkProbeNode runtimeHead = fixture.GetRuntimeNode(head);

            Assert.That(runtimeHead.direct.HasEditorReference, Is.True);
            Assert.That(runtimeHead.direct.HasReference, Is.False);
            Assert.That(runtimeHead.direct.Node, Is.Null);
            Assert.That(runtimeHead.children[0].HasEditorReference, Is.True);
            Assert.That(runtimeHead.children[0].HasReference, Is.False);
            Assert.That(runtimeHead.children[0].Node, Is.Null);
            Assert.That(runtimeHead.raw.HasEditorReference, Is.True);
            Assert.That(runtimeHead.raw.HasReference, Is.False);
            Assert.That(runtimeHead.raw.Node, Is.Null);
            Assert.That(runtimeHead.weighted[0].HasEditorReference, Is.True);
            Assert.That(runtimeHead.weighted[0].HasReference, Is.False);
            Assert.That(runtimeHead.weighted[0].Node, Is.Null);
            Assert.That(runtimeHead.pseudoWeighted[0].HasEditorReference, Is.True);
            Assert.That(runtimeHead.pseudoWeighted[0].HasReference, Is.False);
            Assert.That(runtimeHead.pseudoWeighted[0].Node, Is.Null);
        }

        [UnityTest]
        public IEnumerator GetNode_BindsNodeReferencesOnDemand()
        {
            LinkProbeNode target = CreateNode<LinkProbeNode>("target");
            LinkProbeNode rawTarget = CreateNode<LinkProbeNode>("raw-target");
            LinkProbeNode head = CreateNode<LinkProbeNode>("head");
            head.direct = new NodeReference(target.uuid);
            head.raw = new RawNodeReference { UUID = rawTarget.uuid };

            using TreeFixture fixture = CreateFixture(head, target, rawTarget);
            yield return fixture.WaitUntilReady();

            LinkProbeNode runtimeHead = fixture.GetRuntimeNode(head);
            TreeNode runtimeTarget = fixture.GetRuntimeNode(target);
            TreeNode runtimeRawTarget = fixture.GetRuntimeNode(rawTarget);

            Assert.That(runtimeHead.direct.Node, Is.Null);
            Assert.That(runtimeHead.raw.Node, Is.Null);

            Assert.That(fixture.Tree.GetNode(runtimeHead.direct), Is.SameAs(runtimeTarget));
            Assert.That(runtimeHead.direct.Node, Is.SameAs(runtimeTarget));
            Assert.That(fixture.Tree.GetNode(runtimeHead.raw), Is.SameAs(runtimeRawTarget));
            Assert.That(runtimeHead.raw.Node, Is.SameAs(runtimeRawTarget));
        }

        [UnityTest]
        public IEnumerator FlowNodes_ResolveChildReferencesWhenExecuting()
        {
            LinkProbeNode target = CreateNode<LinkProbeNode>("target");
            Always always = CreateNode<Always>("always");
            always.node = new NodeReference(target.uuid);
            Inverter inverter = CreateNode<Inverter>("inverter");
            inverter.node = new NodeReference(target.uuid);
            Condition condition = CreateNode<Condition>("condition");
            condition.trueNode = new NodeReference(target.uuid);

            using TreeFixture fixture = CreateFixture(always, target, inverter, condition);
            yield return fixture.WaitUntilReady();

            TreeNode runtimeTarget = fixture.GetRuntimeNode(target);
            Always runtimeAlways = fixture.GetRuntimeNode(always);
            Inverter runtimeInverter = fixture.GetRuntimeNode(inverter);
            Condition runtimeCondition = fixture.GetRuntimeNode(condition);

            Assert.That(runtimeAlways.node.HasReference, Is.False);
            BehaviourTree.NodeCallStack alwaysStack = PrepareStack(runtimeAlways);
            Assert.That(runtimeAlways.Execute(), Is.EqualTo(State.NONE_RETURN));
            Assert.That(runtimeAlways.node.Node, Is.SameAs(runtimeTarget));
            Assert.That(alwaysStack.Peek(), Is.SameAs(runtimeTarget));

            Assert.That(runtimeInverter.node.HasReference, Is.False);
            BehaviourTree.NodeCallStack inverterStack = PrepareStack(runtimeInverter);
            Assert.That(runtimeInverter.Execute(), Is.EqualTo(State.NONE_RETURN));
            Assert.That(runtimeInverter.node.Node, Is.SameAs(runtimeTarget));
            Assert.That(inverterStack.Peek(), Is.SameAs(runtimeTarget));

            Assert.That(runtimeCondition.trueNode.HasReference, Is.False);
            BehaviourTree.NodeCallStack conditionStack = PrepareStack(runtimeCondition);
            Assert.That(runtimeCondition.ReceiveReturnFromChild(true), Is.EqualTo(State.NONE_RETURN));
            Assert.That(runtimeCondition.trueNode.Node, Is.SameAs(runtimeTarget));
            Assert.That(conditionStack.Peek(), Is.SameAs(runtimeTarget));
        }

        [UnityTest]
        public IEnumerator RequiredConditionNodes_ThrowWhenLookupCannotResolveReference()
        {
            WaitUntil waitUntil = CreateNode<WaitUntil>("wait-until");
            waitUntil.condition = new NodeReference(UUID.NewUUID());
            Interrupt interrupt = CreateNode<Interrupt>("interrupt");
            interrupt.condition = new NodeReference(UUID.NewUUID());

            using TreeFixture fixture = CreateFixture(
                waitUntil,
                Array.Empty<VariableData>(),
                NodeErrorSolution.Throw,
                interrupt);
            yield return fixture.WaitUntilReady();

            Assert.Throws<InvalidNodeException>(() => fixture.GetRuntimeNode(waitUntil).Execute());
            Assert.Throws<InvalidNodeException>(() => fixture.GetRuntimeNode(interrupt).Execute());
        }

        [Test]
        public void EditorCheck_UsesSerializedNodeReferenceState()
        {
            WaitUntil waitUntil = CreateNode<WaitUntil>("wait-until");
            waitUntil.condition = new NodeReference(UUID.NewUUID());
            Rollback rollback = CreateNode<Rollback>("rollback");
            rollback.stopAt = new RawNodeReference { UUID = UUID.NewUUID() };

            Assert.That(waitUntil.EditorCheck(null), Is.True);
            Assert.That(rollback.EditorCheck(null), Is.True);

            waitUntil.condition = NodeReference.Empty;
            rollback.stopAt = RawNodeReference.Empty;

            Assert.That(waitUntil.EditorCheck(null), Is.False);
            Assert.That(rollback.EditorCheck(null), Is.False);
        }

        [UnityTest]
        public IEnumerator LinkReference_InitializesDirectVariablesAndParameterLists()
        {
            VariableData variable = new("count", VariableType.Int) { DefaultValue = "7" };
            LinkProbeNode head = CreateNode<LinkProbeNode>("head");
            head.directVariable.SetReference(variable);
            head.parameters.Add(CreateParameter(variable));

            using TreeFixture fixture = CreateFixture(head, new[] { variable });
            yield return fixture.WaitUntilReady();

            LinkProbeNode runtimeHead = fixture.GetRuntimeNode(head);

            Assert.That(runtimeHead.directVariable.HasReference, Is.True);
            Assert.That(runtimeHead.directVariable.IntValue, Is.EqualTo(7));
            Assert.That(runtimeHead.parameters[0].HasReference, Is.True);
            Assert.That(runtimeHead.parameters[0].IntValue, Is.EqualTo(7));
        }

        private static BehaviourTree.NodeCallStack PrepareStack(TreeNode node)
        {
            BehaviourTree.NodeCallStack stack = new();
            stack.Initialize();
            node.callStack = stack;
            return stack;
        }

        private static Parameter CreateParameter(VariableData variable)
        {
            Parameter parameter = new(variable.Type);
            parameter.SetReference(variable);
            return parameter;
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

        private static TreeFixture CreateFixture(TreeNode head, params TreeNode[] nodes)
        {
            return CreateFixture(head, Array.Empty<VariableData>(), NodeErrorSolution.False, nodes);
        }

        private static TreeFixture CreateFixture(TreeNode head, VariableData[] variables, params TreeNode[] nodes)
        {
            return CreateFixture(head, variables, NodeErrorSolution.False, nodes);
        }

        private static TreeFixture CreateFixture(
            TreeNode head,
            VariableData[] variables,
            NodeErrorSolution nodeErrorSolution,
            params TreeNode[] nodes)
        {
            BehaviourTreeData data = ScriptableObject.CreateInstance<BehaviourTreeData>();
            data.noActionMaximumDurationLimit = true;
            data.nodeErrorHandle = nodeErrorSolution;
            data.headNodeUUID = head.uuid;
            data.variables.AddRange(variables);
            data.nodes.Add(head);

            foreach (TreeNode node in nodes)
            {
                if (node.uuid != head.uuid)
                {
                    data.nodes.Add(node);
                }
            }

            GameObject gameObject = new("BehaviourTreeReferenceInitializationTests");
            TestBehaviour script = gameObject.AddComponent<TestBehaviour>();
            BehaviourTree tree = new(data, gameObject, script);
            return new TreeFixture(data, gameObject, tree);
        }

        [DoNotRelease]
        [Serializable]
        public sealed class LinkProbeNode : TreeNode
        {
            public NodeReference direct = new();
            public NodeReference[] children = Array.Empty<NodeReference>();
            public RawNodeReference raw = new();
            public Probability.EventWeight[] weighted = Array.Empty<Probability.EventWeight>();
            public PseudoProbability.EventWeight[] pseudoWeighted = Array.Empty<PseudoProbability.EventWeight>();
            public VariableReference directVariable = new();
            public List<Parameter> parameters = new();

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Success;
            }
        }

        private sealed class TestBehaviour : MonoBehaviour
        {
        }

        private sealed class TreeFixture : IDisposable
        {
            private readonly BehaviourTreeData data;
            private readonly GameObject gameObject;

            public TreeFixture(BehaviourTreeData data, GameObject gameObject, BehaviourTree tree)
            {
                this.data = data;
                this.gameObject = gameObject;
                Tree = tree;
            }

            public BehaviourTree Tree { get; }

            public T GetRuntimeNode<T>(T prototype) where T : TreeNode
            {
                return (T)Tree.References[prototype.uuid]!;
            }

            public IEnumerator WaitUntilReady()
            {
                float timeout = Time.realtimeSinceStartup + 3f;
                while (!Tree.IsInitialized && !Tree.IsError && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                Assert.That(Tree.IsInitialized, Is.True);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
