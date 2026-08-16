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

        [Test]
        public void NodeReferenceLayout_ChangesAt360PixelBreakpoint()
        {
            Assert.That(GraphInspectorLayout.UseWideNodeReferenceLayout(359.99f), Is.False);
            Assert.That(GraphInspectorLayout.UseWideNodeReferenceLayout(360f), Is.True);
        }

        [Test]
        public void NodeReferenceLayout_UsesDirectActionsAtWideWidth()
        {
            GraphInspectorLayout.NodeReferenceRects layout = GraphInspectorLayout.CalculateNodeReferenceRects(
                new Rect(0f, 0f, 420f, 18f), true, true);

            Assert.That(layout.IndexRect.width, Is.EqualTo(28f));
            Assert.That(layout.NameRect.xMax, Is.LessThanOrEqualTo(layout.OpenRect.xMin));
            Assert.That(layout.OpenRect.xMax, Is.LessThanOrEqualTo(layout.DeleteRect.xMin));
            Assert.That(layout.DeleteRect.xMax, Is.EqualTo(420f));
            Assert.That(layout.OverflowRect, Is.EqualTo(Rect.zero));
        }

        [Test]
        public void NodeReferenceLayout_UsesOverflowAtNarrowWidth()
        {
            GraphInspectorLayout.NodeReferenceRects layout = GraphInspectorLayout.CalculateNodeReferenceRects(
                new Rect(10f, 2f, 220f, 18f), true, false);

            Assert.That(layout.NameRect.xMax, Is.LessThanOrEqualTo(layout.OverflowRect.xMin));
            Assert.That(layout.OverflowRect.width, Is.EqualTo(22f));
            Assert.That(layout.OpenRect, Is.EqualTo(Rect.zero));
            Assert.That(layout.DeleteRect, Is.EqualTo(Rect.zero));
            Assert.That(layout.OverflowRect.xMax, Is.EqualTo(230f));
        }

        [Test]
        public void NodeReferenceLayout_UnstableOccurrenceHasNoActions()
        {
            GraphInspectorLayout.NodeReferenceRects layout = GraphInspectorLayout.CalculateNodeReferenceRects(
                new Rect(10f, 2f, 220f, 18f), false, false);

            Assert.That(layout.NameRect.xMax, Is.EqualTo(230f));
            Assert.That(layout.OpenRect, Is.EqualTo(Rect.zero));
            Assert.That(layout.DeleteRect, Is.EqualTo(Rect.zero));
            Assert.That(layout.OverflowRect, Is.EqualTo(Rect.zero));
        }

        [Test]
        public void NodeReferenceListBodyLayout_EmptyListSkipsTreeViewBody()
        {
            NodeDrawerBase.NodeReferenceTreeView.NodeReferenceListBodyLayout layout =
                NodeDrawerBase.NodeReferenceTreeView.CalculateBodyLayout(0, 128f);

            Assert.That(layout.DrawTreeView, Is.False);
            Assert.That(layout.Height, Is.EqualTo(0f));
        }

        [Test]
        public void NodeReferenceListBodyLayout_NonEmptyListClampsBodyToMinAndMax()
        {
            NodeDrawerBase.NodeReferenceTreeView.NodeReferenceListBodyLayout belowMinimum =
                NodeDrawerBase.NodeReferenceTreeView.CalculateBodyLayout(1, -100f);
            NodeDrawerBase.NodeReferenceTreeView.NodeReferenceListBodyLayout aboveMaximum =
                NodeDrawerBase.NodeReferenceTreeView.CalculateBodyLayout(1, 1000f);

            Assert.That(belowMinimum.DrawTreeView, Is.True);
            Assert.That(belowMinimum.Height, Is.EqualTo(24f));
            Assert.That(aboveMaximum.DrawTreeView, Is.True);
            Assert.That(aboveMaximum.Height, Is.EqualTo(320f));
        }
    }
}
