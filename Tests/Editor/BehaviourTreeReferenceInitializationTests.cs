#nullable enable
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.TestTools;
using WaitUntil = Aethiumian.AI.Nodes.WaitUntil;

namespace Aethiumian.AI.Tests
{
    public sealed class BehaviourTreeReferenceInitializationTests
    {
        [UnityTest]
        public IEnumerator LinkReference_DoesNotEagerBindNodeReferences()
        {
            LinkProbeNode target = TreeTestFixture.CreateNode<LinkProbeNode>("target");
            LinkProbeNode rawTarget = TreeTestFixture.CreateNode<LinkProbeNode>("raw-target");
            LinkProbeNode head = TreeTestFixture.CreateNode<LinkProbeNode>("head");
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

            using var fixture = TreeTestFixture.Create(head, target, rawTarget);
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
            LinkProbeNode target = TreeTestFixture.CreateNode<LinkProbeNode>("target");
            LinkProbeNode rawTarget = TreeTestFixture.CreateNode<LinkProbeNode>("raw-target");
            LinkProbeNode head = TreeTestFixture.CreateNode<LinkProbeNode>("head");
            head.direct = new NodeReference(target.uuid);
            head.raw = new RawNodeReference { UUID = rawTarget.uuid };

            using var fixture = TreeTestFixture.Create(head, target, rawTarget);
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
            LinkProbeNode target = TreeTestFixture.CreateNode<LinkProbeNode>("target");
            Always always = TreeTestFixture.CreateNode<Always>("always");
            always.node = new NodeReference(target.uuid);
            Inverter inverter = TreeTestFixture.CreateNode<Inverter>("inverter");
            inverter.node = new NodeReference(target.uuid);
            Condition condition = TreeTestFixture.CreateNode<Condition>("condition");
            condition.trueNode = new NodeReference(target.uuid);

            using var fixture = TreeTestFixture.Create(always, target, inverter, condition);
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
            WaitUntil waitUntil = TreeTestFixture.CreateNode<WaitUntil>("wait-until");
            waitUntil.condition = new NodeReference(UUID.NewUUID());
            Interrupt interrupt = TreeTestFixture.CreateNode<Interrupt>("interrupt");
            interrupt.condition = new NodeReference(UUID.NewUUID());

            using var fixture = TreeTestFixture.Create(
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
            WaitUntil waitUntil = TreeTestFixture.CreateNode<WaitUntil>("wait-until");
            waitUntil.condition = new NodeReference(UUID.NewUUID());
            Rollback rollback = TreeTestFixture.CreateNode<Rollback>("rollback");
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
            VariableData variable = new("count", VariableType.Int);
            variable.SetDefaultValue(7);
            VariableData fieldVariable = new("field", VariableType.Int);
            fieldVariable.SetDefaultValue(11);
            VariableData weightVariable = new("weight", VariableType.Int);
            weightVariable.SetDefaultValue(13);
            LinkProbeNode head = TreeTestFixture.CreateNode<LinkProbeNode>("head");
            head.directVariable.SetReference(variable);
            head.parameters.Add(CreateParameter(variable));
            head.fieldPointers.Add(new FieldPointer { name = "field", data = CreateVariableReference(fieldVariable) });
            head.fieldData.Add(new FieldChangeData { name = "field", data = CreateParameter(fieldVariable) });
            head.pseudoWeighted = new[]
            {
                new PseudoProbability.EventWeight
                {
                    weight = CreateVariableField(weightVariable),
                    reference = NodeReference.Empty,
                },
            };

            using var fixture = TreeTestFixture.Create(head, new[] { variable, fieldVariable, weightVariable });
            yield return fixture.WaitUntilReady();

            LinkProbeNode runtimeHead = fixture.GetRuntimeNode(head);

            Assert.That(runtimeHead.directVariable.HasReference, Is.True);
            Assert.That(runtimeHead.directVariable.IntValue, Is.EqualTo(7));
            Assert.That(runtimeHead.parameters[0].HasReference, Is.True);
            Assert.That(runtimeHead.parameters[0].IntValue, Is.EqualTo(7));
            Assert.That(runtimeHead.fieldPointers[0].data.HasReference, Is.True);
            Assert.That(runtimeHead.fieldPointers[0].data.IntValue, Is.EqualTo(11));
            Assert.That(runtimeHead.fieldData[0].data.HasReference, Is.True);
            Assert.That(runtimeHead.fieldData[0].data.IntValue, Is.EqualTo(11));
            Assert.That(runtimeHead.pseudoWeighted[0].weight.HasReference, Is.True);
            Assert.That(runtimeHead.pseudoWeighted[0].weight.IntValue, Is.EqualTo(13));
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

        private static VariableReference CreateVariableReference(VariableData variable)
        {
            VariableReference reference = new() { type = variable.Type };
            reference.SetReference(variable);
            return reference;
        }

        private static VariableField<int> CreateVariableField(VariableData variable)
        {
            VariableField<int> field = new();
            field.SetReference(variable);
            return field;
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
            public List<FieldPointer> fieldPointers = new();
            public List<FieldChangeData> fieldData = new();

            public override void Initialize()
            {
            }

            public override State Execute()
            {
                return State.Success;
            }
        }

    }
}
