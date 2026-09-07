using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

using Aethiumian.AI.Editor.Tests.Support;

namespace Aethiumian.AI.Editor.Tests.ComponentValue
{
    /// <summary>Verifies component-value nodes against the tree's attached GameObject.</summary>
    public sealed class ComponentValueTests
    {
        [UnityTest]
        public IEnumerator SetAndGetComponentValue_UsesAttachedComponentAndTreeVariable()
        {
            Sequence sequence = TreeTestFixture.CreateNode<Sequence>("Component values");
            SetComponentValue set = TreeTestFixture.CreateNode<SetComponentValue>("Set value");
            GetComponentValue get = TreeTestFixture.CreateNode<GetComponentValue>("Get value");
            VariableData readback = new("Readback", VariableType.Int);
            readback.SetDefaultValue(-1);

            sequence.events = new[] { set.ToReference(), get.ToReference() };
            set.parent = sequence.ToReference();
            get.parent = sequence.ToReference();

            set.getComponent = true;
            set.type = CreateComponentTypeReference();
            set.AddChangeEntry(nameof(ComponentProbe.value), VariableType.Int).data = new Parameter(42);

            get.getComponent = true;
            get.type = CreateComponentTypeReference();
            get.AddPointer(nameof(ComponentProbe.value), VariableType.Int);
            get.GetChangeEntry(nameof(ComponentProbe.value)).SetReference(readback);

            using TreeTestFixture fixture = TreeTestFixture.Create(sequence, new[] { readback }, set, get);
            ComponentProbe probe = fixture.GameObject.AddComponent<ComponentProbe>();
            probe.value = 7;
            yield return fixture.WaitUntilReady();

            fixture.Start();
            yield return fixture.WaitUntil(() => fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End, 1f);

            Assert.That(fixture.Tree.MainStack.ReturnValue, Is.True);
            Assert.That(probe.value, Is.EqualTo(42));
            Assert.That(fixture.Tree.Variables[readback.UUID].IntValue, Is.EqualTo(42));
        }

        private static TypeReference<Component> CreateComponentTypeReference()
        {
            TypeReference<Component> reference = new();
            Assert.That(reference.SetReferType(typeof(ComponentProbe)), Is.True);
            return reference;
        }

        private sealed class ComponentProbe : MonoBehaviour
        {
            public int value;
        }
    }
}
