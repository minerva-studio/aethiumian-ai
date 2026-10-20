using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Exercises displacement conditions and vector storage through real runtime bindings.</summary>
    public sealed class DisplacementToTests
    {
        private GameObject target;
        private DisplacementTo prototype;
        private DisplacementTo runtime;
        private TreeTestFixture fixture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            target = new GameObject("DisplacementTarget");
            target.transform.position = new Vector3(3f, -4f, 80f);
            prototype = TreeTestFixture.CreateNode<DisplacementTo>("Displacement To");
            VariableData targetVariable = new("Target", VariableType.UnityObject);
            VariableData displacementVariable = new("Displacement", VariableType.Vector2);
            prototype.@object.SetReference(targetVariable);
            prototype.result.SetReference(displacementVariable);
            fixture = TreeTestFixture.Create(prototype, new[]
            {
                targetVariable, displacementVariable, new VariableData("Threshold", VariableType.Float),
            });
            yield return fixture.WaitUntilReady();
            fixture.Tree.SetVariable("Target", target);
            runtime = fixture.GetRuntimeNode(prototype);
        }

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            if (target) Object.DestroyImmediate(target);
        }

        [Test]
        public void StoredDisplacement_UsesTargetMinusSourceInWorldXY()
        {
            fixture.GameObject.transform.position = new Vector3(10f, 20f, -100f);
            target.transform.position = new Vector3(13f, 16f, 100f);
            runtime.storeResult = true;
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(new Vector2(3f, -4f)));
        }

        [TestCase(false, false, -3f, -4f)]
        [TestCase(true, false, 3f, -4f)]
        [TestCase(false, true, -3f, 4f)]
        [TestCase(true, true, 3f, 4f)]
        public void Absolute_AffectsEachComparisonButPreservesSignedOutput(bool absoluteX, bool absoluteY, float expectedX, float expectedY)
        {
            target.transform.position = new Vector3(-3f, -4f, 0f);
            runtime.absoluteX = absoluteX;
            runtime.absoluteY = absoluteY;
            runtime.compareX = runtime.compareY = runtime.storeResult = true;
            runtime.xMode = runtime.yMode = CompareSign.equals;
            runtime.xExpect = expectedX;
            runtime.yExpect = expectedY;
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(new Vector2(-3f, -4f)));
        }

        [TestCase(CompareSign.less, 4f, true)]
        [TestCase(CompareSign.less, 3f, false)]
        [TestCase(CompareSign.lessOrEquals, 3f, true)]
        [TestCase(CompareSign.lessOrEquals, 2f, false)]
        [TestCase(CompareSign.equals, 3f, true)]
        [TestCase(CompareSign.equals, 4f, false)]
        [TestCase(CompareSign.notEquals, 3f, false)]
        [TestCase(CompareSign.notEquals, 4f, true)]
        [TestCase(CompareSign.greaterOrEquals, 3f, true)]
        [TestCase(CompareSign.greaterOrEquals, 4f, false)]
        [TestCase(CompareSign.greater, 2f, true)]
        [TestCase(CompareSign.greater, 3f, false)]
        public void SingleAxis_UsesConfiguredOperatorAndBoundary(CompareSign mode, float threshold, bool success)
        {
            runtime.compareX = true;
            runtime.xMode = mode;
            runtime.xExpect = threshold;
            Assert.That(runtime.Execute(), Is.EqualTo(success ? State.Success : State.Failed));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void TwoAxes_RequireBothEnabledComparisonsToPass(bool xPasses, bool yPasses, bool success)
        {
            runtime.compareX = runtime.compareY = true;
            runtime.xMode = runtime.yMode = CompareSign.equals;
            runtime.xExpect = xPasses ? 3f : 30f;
            runtime.yExpect = yPasses ? -4f : -40f;
            Assert.That(runtime.Execute(), Is.EqualTo(success ? State.Success : State.Failed));
        }

        [Test]
        public void DisabledAxis_IsExcludedAndItsThresholdIsNotRead()
        {
            runtime.compareY = true;
            runtime.yMode = CompareSign.equals;
            runtime.yExpect = -4f;
            runtime.xExpect.SetReference(new VariableData("Unresolved unused threshold", VariableType.Float));
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
        }

        [TestCase(2f, true)]
        [TestCase(1f, false)]
        [TestCase(-2f, false)]
        public void MixedAbsoluteAndSignedAxes_PreserveVerticalDirection(float targetY, bool success)
        {
            target.transform.position = new Vector3(-3f, targetY, 0f);
            runtime.compareX = runtime.compareY = true;
            runtime.absoluteX = true;
            runtime.xMode = CompareSign.less;
            runtime.xExpect = 6f;
            runtime.yMode = CompareSign.greater;
            runtime.yExpect = 1f;
            Assert.That(runtime.Execute(), Is.EqualTo(success ? State.Success : State.Failed));
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, false)]
        public void ZeroDisplacement_IsAValidMeasurement(bool compareX, bool compareY, bool success)
        {
            target.transform.position = fixture.GameObject.transform.position;
            runtime.compareX = compareX;
            runtime.compareY = compareY;
            runtime.xMode = CompareSign.lessOrEquals;
            runtime.yMode = CompareSign.greater;
            runtime.xExpect = runtime.yExpect = 0f;
            runtime.storeResult = true;
            Assert.That(runtime.Execute(), Is.EqualTo(success ? State.Success : State.Failed));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void FailedComparison_StillStoresTheMeasuredDisplacement()
        {
            runtime.compareX = runtime.storeResult = true;
            runtime.xMode = CompareSign.less;
            runtime.xExpect = 0f;
            Assert.That(runtime.Execute(), Is.EqualTo(State.Failed));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(new Vector2(3f, -4f)));
        }

        [Test]
        public void StorageDisabled_PreservesThePreviousOutput()
        {
            fixture.Tree.SetVariable("Displacement", Vector2.one);
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void OptionalUnboundOutput_DoesNotPreventEvaluation()
        {
            runtime.storeResult = true;
            runtime.result.SetReference(null);
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDestroyedTarget_FailsWithoutWriting(bool destroy)
        {
            runtime.storeResult = true;
            fixture.Tree.SetVariable("Displacement", Vector2.one);
            if (destroy) Object.DestroyImmediate(target);
            else fixture.Tree.SetVariable<GameObject>("Target", null);
            Assert.That(runtime.Execute(), Is.EqualTo(State.Failed));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void UnboundTarget_UsesTheConfiguredExceptionRule()
        {
            fixture.Data.nodeErrorHandle = NodeErrorSolution.Throw;
            runtime.@object.SetReference(null);
            Assert.Throws<InvalidNodeException>(() => runtime.Execute());
        }

        [Test]
        public void RepeatedExecution_ReadsTheTargetsCurrentPosition()
        {
            runtime.storeResult = true;
            runtime.Execute();
            target.transform.position = new Vector3(-8f, 2f, 0f);
            Assert.That(runtime.Execute(), Is.EqualTo(State.Success));
            Assert.That(runtime.result.Vector2Value, Is.EqualTo(new Vector2(-8f, 2f)));
        }

        [UnityTest]
        public IEnumerator BoundThreshold_IsResolvedAndReadOnEachExecution()
        {
            prototype.compareX = true;
            prototype.xMode = CompareSign.lessOrEquals;
            prototype.xExpect.SetReference(fixture.Data.variables.Find(variable => variable.name == "Threshold"));
            using TreeTestFixture other = TreeTestFixture.Create(prototype, fixture.Data.variables.ToArray());
            yield return other.WaitUntilReady();
            other.Tree.SetVariable("Target", target);
            other.Tree.SetVariable("Threshold", 4f);
            DisplacementTo node = other.GetRuntimeNode(prototype);
            Assert.That(node.Execute(), Is.EqualTo(State.Success));
            other.Tree.SetVariable("Threshold", 2f);
            Assert.That(node.Execute(), Is.EqualTo(State.Failed));
            Assert.That(prototype.xExpect.RuntimeVariable, Is.Null);
        }

        [Test]
        public void Duplicate_CopiesBindingsWithoutSharingFieldInstances()
        {
            DisplacementTo clone = (DisplacementTo)NodeFactory.Duplicate(prototype);
            Assert.That(clone.xExpect, Is.Not.SameAs(prototype.xExpect));
            Assert.That(clone.yExpect, Is.Not.SameAs(prototype.yExpect));
            Assert.That(clone.result, Is.Not.SameAs(prototype.result));
            Assert.That(clone.result.UUID, Is.EqualTo(prototype.result.UUID));
            clone.xExpect = 10f;
            Assert.That(prototype.xExpect.Constant, Is.EqualTo(0f));
        }

        [Test]
        public void Serialization_PreservesComparisonOptionsAndOutputBinding()
        {
            prototype.compareX = prototype.absoluteX = prototype.storeResult = true;
            prototype.xMode = CompareSign.greaterOrEquals;
            prototype.xExpect = 2f;
            DisplacementTo restored = JsonUtility.FromJson<DisplacementTo>(JsonUtility.ToJson(prototype));
            Assert.That(restored.compareX, Is.True);
            Assert.That(restored.absoluteX, Is.True);
            Assert.That(restored.compareY, Is.False);
            Assert.That(restored.storeResult, Is.True);
            Assert.That(restored.xMode, Is.EqualTo(CompareSign.greaterOrEquals));
            Assert.That(restored.xExpect.Constant, Is.EqualTo(2f));
            Assert.That(restored.result.UUID, Is.EqualTo(prototype.result.UUID));
        }
    }
}
