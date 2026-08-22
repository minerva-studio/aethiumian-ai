using Aethiumian.AI.Editor;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System;
using System.Reflection;

namespace Aethiumian.AI.Editor.Tests.NodeDrawers
{
    /// <summary>Validates source-shape rules used by the CreateVector node drawers.</summary>
    public sealed class VectorConstructorDrawerTests
    {
        [Test]
        public void VectorLaneContainsOnlyNativeLanes()
        {
            Assert.That(Enum.GetNames(typeof(VectorLane)), Is.EqualTo(new[] { "X", "Y", "Z", "W" }));
        }

        [TestCase(VariableType.Invalid, 0)]
        [TestCase(VariableType.Int, 0)]
        [TestCase(VariableType.Float, 0)]
        [TestCase(VariableType.Bool, 0)]
        [TestCase(VariableType.Vector2, 2)]
        [TestCase(VariableType.Vector3, 3)]
        [TestCase(VariableType.Vector4, 4)]
        public void LaneCountMatchesSourceShape(VariableType type, int expectedCount)
        {
            Assert.That(VectorConstructorDrawerBase.GetLaneCount(type), Is.EqualTo(expectedCount));
        }

        [Test]
        public void InvalidLaneNormalizesToX()
        {
            Assert.That(VectorConstructorDrawerBase.NormalizeLaneIndex(3, 2, out int normalized), Is.True);
            Assert.That(normalized, Is.EqualTo(0));

            Assert.That(VectorConstructorDrawerBase.NormalizeLaneIndex(2, 3, out normalized), Is.False);
            Assert.That(normalized, Is.EqualTo(2));

            Assert.That(VectorConstructorDrawerBase.NormalizeLaneIndex(0, 0, out normalized), Is.False);
            Assert.That(normalized, Is.EqualTo(0));
        }

        [Test]
        public void ScalarAndInvalidSourcesHideLaneByReturningZeroComponents()
        {
            Assert.That(VectorConstructorDrawerBase.GetLaneCount(VariableType.Int), Is.Zero);
            Assert.That(VectorConstructorDrawerBase.GetLaneCount(VariableType.Float), Is.Zero);
            Assert.That(VectorConstructorDrawerBase.GetLaneCount(VariableType.Bool), Is.Zero);
            Assert.That(VectorConstructorDrawerBase.GetLaneCount(VariableType.Invalid), Is.Zero);
        }

        [Test]
        public void ScalarSourcesIgnoreLaneSelectionAndUnknownValuesFail()
        {
            VariableField scalar = new(VariableType.Float);
            scalar.ForceSetConstantValue(2.5f);

            Assert.That(scalar.TryGetVectorLane(VectorLane.W, out float value), Is.True);
            Assert.That(value, Is.EqualTo(2.5f));
            Assert.That(scalar.TryGetVectorLane((VectorLane)4, out _), Is.False);
        }

        [Test]
        public void CreateVectorNodesHaveDedicatedDrawers()
        {
            AssertDrawer<CreateVector2, CreateVector2Drawer>();
            AssertDrawer<CreateVector3, CreateVector3Drawer>();
            AssertDrawer<CreateVector4, CreateVector4Drawer>();
        }

        [Test]
        public void CreateVectorFieldOrderKeepsResultAfterAllSources()
        {
            Assert.That(
                VectorConstructorDrawerBase.GetFieldOrder(2),
                Is.EqualTo(new[] { "failOnNaN", "x", "xLane", "y", "yLane", "vector" }));
            Assert.That(
                VectorConstructorDrawerBase.GetFieldOrder(3),
                Is.EqualTo(new[] { "failOnNaN", "x", "xLane", "y", "yLane", "z", "zLane", "vector" }));
            Assert.That(
                VectorConstructorDrawerBase.GetFieldOrder(4),
                Is.EqualTo(new[] { "failOnNaN", "x", "xLane", "y", "yLane", "z", "zLane", "w", "wLane", "vector" }));
        }

        private static void AssertDrawer<TNode, TDrawer>()
            where TNode : TreeNode
            where TDrawer : NodeDrawerBase
        {
            CustomNodeDrawerAttribute attribute = typeof(TDrawer).GetCustomAttribute<CustomNodeDrawerAttribute>();
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute.type, Is.EqualTo(typeof(TNode)));
        }
    }
}
