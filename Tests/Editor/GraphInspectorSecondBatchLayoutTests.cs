using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Validates second-batch Graph Inspector layout calculations.</summary>
    public sealed class GraphInspectorSecondBatchLayoutTests
    {
        [Test]
        public void MethodRow_At220Pixels_HasNonOverlappingMainAndOverflowRects()
        {
            GraphInspectorLayout.FunctionSelectionRects layout =
                GraphInspectorLayout.CalculateFunctionSelectionRects(new Rect(0f, 0f, 220f, 18f));

            Assert.That(layout.ValueRect.width, Is.EqualTo(198f));
            Assert.That(layout.OverflowRect.width, Is.EqualTo(22f));
            Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.ValueRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
            Assert.That(layout.OverflowRect.xMax, Is.EqualTo(220f));
        }

        [Test]
        public void TypeReferenceRow_At220Pixels_HasNonOverlappingValuePickAndOverflowRects()
        {
            GraphInspectorLayout.TypeReferenceRects layout =
                GraphInspectorLayout.CalculateTypeReferenceRects(new Rect(0f, 0f, 220f, 18f));

            Assert.That(layout.ValueRect.width, Is.EqualTo(146f));
            Assert.That(layout.PickRect.width, Is.EqualTo(52f));
            Assert.That(layout.OverflowRect.width, Is.EqualTo(22f));
            Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.ValueRect.xMax, Is.LessThanOrEqualTo(layout.PickRect.xMin));
            Assert.That(layout.PickRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
            Assert.That(layout.OverflowRect.xMax, Is.EqualTo(220f));
        }
    }
}
