using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Validates the pure layout contracts used by Graph Inspector drawers.</summary>
    public sealed class GraphInspectorLayoutTests
    {
        [Test]
        public void ResponsiveLayout_UsesNonOverlappingRectsAtNarrowWidth()
        {
            ResponsiveIMGUILayout layout = ResponsiveIMGUILayout.CalculateSingleLine(new Rect(0f, 0f, 220f, 18f));

            Assert.That(layout.LabelRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.OverflowRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.LabelRect.xMax, Is.LessThanOrEqualTo(layout.ValueRect.xMin));
            Assert.That(layout.ValueRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
            Assert.That(layout.OverflowRect.xMax, Is.LessThanOrEqualTo(220f));
        }

        [Test]
        public void FunctionSelectionLayout_UsesAllWidthExceptOverflow()
        {
            GraphInspectorLayout.FunctionSelectionRects layout =
                GraphInspectorLayout.CalculateFunctionSelectionRects(new Rect(10f, 4f, 220f, 18f));

            Assert.That(layout.ValueRect.width, Is.EqualTo(198f));
            Assert.That(layout.ValueRect.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(layout.OverflowRect.width, Is.EqualTo(22f));
            Assert.That(layout.ValueRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
            Assert.That(layout.OverflowRect.xMax, Is.EqualTo(230f));
        }

        [Test]
        public void SubtreeTranslationLayout_ChangesAt360PixelBreakpoint()
        {
            Assert.That(GraphInspectorLayout.UseWideSubtreeTranslationLayout(359.99f), Is.False);
            Assert.That(GraphInspectorLayout.UseWideSubtreeTranslationLayout(360f), Is.True);
        }
    }
}
