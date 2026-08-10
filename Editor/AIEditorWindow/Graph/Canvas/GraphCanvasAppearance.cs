using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Holds USS-resolved paint values for one graph canvas without owning layout or topology state.
    /// </summary>
    internal sealed class GraphCanvasAppearance
    {
        private static readonly CustomStyleProperty<Color> GridDarkProperty = new("--graph-grid-dark");
        private static readonly CustomStyleProperty<Color> GridLightProperty = new("--graph-grid-light");
        private static readonly CustomStyleProperty<Color> NormalFillDarkProperty = new("--graph-node-normal-fill-dark");
        private static readonly CustomStyleProperty<Color> NormalFillLightProperty = new("--graph-node-normal-fill-light");
        private static readonly CustomStyleProperty<Color> FlowFillDarkProperty = new("--graph-node-flow-fill-dark");
        private static readonly CustomStyleProperty<Color> FlowFillLightProperty = new("--graph-node-flow-fill-light");
        private static readonly CustomStyleProperty<Color> BranchFillDarkProperty = new("--graph-node-branch-fill-dark");
        private static readonly CustomStyleProperty<Color> BranchFillLightProperty = new("--graph-node-branch-fill-light");
        private static readonly CustomStyleProperty<Color> ServiceFillDarkProperty = new("--graph-node-service-fill-dark");
        private static readonly CustomStyleProperty<Color> ServiceFillLightProperty = new("--graph-node-service-fill-light");
        private static readonly CustomStyleProperty<Color> ConditionFillDarkProperty = new("--graph-condition-fill-dark");
        private static readonly CustomStyleProperty<Color> ConditionFillLightProperty = new("--graph-condition-fill-light");
        private static readonly CustomStyleProperty<Color> CompoundFillDarkProperty = new("--graph-compound-fill-dark");
        private static readonly CustomStyleProperty<Color> CompoundFillLightProperty = new("--graph-compound-fill-light");
        private static readonly CustomStyleProperty<Color> NormalStrokeDarkProperty = new("--graph-node-normal-stroke-dark");
        private static readonly CustomStyleProperty<Color> NormalStrokeLightProperty = new("--graph-node-normal-stroke-light");
        private static readonly CustomStyleProperty<Color> FlowStrokeProperty = new("--graph-node-flow-stroke");
        private static readonly CustomStyleProperty<Color> BranchStrokeProperty = new("--graph-node-branch-stroke");
        private static readonly CustomStyleProperty<Color> ServiceStrokeProperty = new("--graph-node-service-stroke");
        private static readonly CustomStyleProperty<Color> SelectedStrokeProperty = new("--graph-node-selected-stroke");
        private static readonly CustomStyleProperty<Color> WarningStrokeProperty = new("--graph-node-warning-stroke");
        private static readonly CustomStyleProperty<Color> StructuralEdgeProperty = new("--graph-edge-structural");
        private static readonly CustomStyleProperty<Color> ServiceEdgeProperty = new("--graph-edge-service");
        private static readonly CustomStyleProperty<Color> RawEdgeProperty = new("--graph-edge-raw");
        private static readonly CustomStyleProperty<Color> FlowEdgeProperty = new("--graph-edge-flow");
        private static readonly CustomStyleProperty<Color> BranchEdgeProperty = new("--graph-edge-branch");
        private static readonly CustomStyleProperty<Color> ProbabilityEdgeProperty = new("--graph-edge-probability");
        private static readonly CustomStyleProperty<Color> ParallelEdgeProperty = new("--graph-edge-parallel");
        private static readonly CustomStyleProperty<Color> LoopEdgeProperty = new("--graph-edge-loop");
        private static readonly CustomStyleProperty<Color> SequenceScopeProperty = new("--graph-scope-sequence");
        private static readonly CustomStyleProperty<Color> SequenceScopeSelectedProperty = new("--graph-scope-sequence-selected");
        private static readonly CustomStyleProperty<Color> ConditionScopeProperty = new("--graph-scope-condition");
        private static readonly CustomStyleProperty<Color> ConditionScopeSelectedProperty = new("--graph-scope-condition-selected");
        private static readonly CustomStyleProperty<Color> ProbabilityScopeProperty = new("--graph-scope-probability");
        private static readonly CustomStyleProperty<Color> ProbabilityScopeSelectedProperty = new("--graph-scope-probability-selected");
        private static readonly CustomStyleProperty<float> GridLineWidthProperty = new("--graph-grid-line-width");
        private static readonly CustomStyleProperty<float> NodeLineWidthProperty = new("--graph-node-line-width");
        private static readonly CustomStyleProperty<float> SelectedLineWidthProperty = new("--graph-selected-line-width");
        private static readonly CustomStyleProperty<float> ScopeLineWidthProperty = new("--graph-scope-line-width");
        private static readonly CustomStyleProperty<float> SelectedScopeLineWidthProperty = new("--graph-scope-selected-line-width");
        private static readonly CustomStyleProperty<float> AuthoredLineWidthProperty = new("--graph-edge-authored-line-width");
        private static readonly CustomStyleProperty<float> DerivedLineWidthProperty = new("--graph-edge-derived-line-width");
        private static readonly CustomStyleProperty<float> PlaceholderLineWidthProperty = new("--graph-edge-placeholder-line-width");
        private static readonly CustomStyleProperty<float> DerivedMarkLengthProperty = new("--graph-edge-derived-mark-length");
        private static readonly CustomStyleProperty<float> DerivedGapLengthProperty = new("--graph-edge-derived-gap-length");
        private static readonly CustomStyleProperty<float> ControlMarkLengthProperty = new("--graph-edge-control-mark-length");
        private static readonly CustomStyleProperty<float> ControlGapLengthProperty = new("--graph-edge-control-gap-length");
        private static readonly CustomStyleProperty<float> PlaceholderMarkLengthProperty = new("--graph-edge-placeholder-mark-length");
        private static readonly CustomStyleProperty<float> PlaceholderGapLengthProperty = new("--graph-edge-placeholder-gap-length");
        private static readonly CustomStyleProperty<float> DisabledAlphaProperty = new("--graph-edge-disabled-alpha");

        /// <summary>Initializes the appearance with the pre-USS paint defaults.</summary>
        internal GraphCanvasAppearance()
        {
            ResetToDefaults();
        }

        internal Color GridDark { get; private set; }
        internal Color GridLight { get; private set; }
        internal Color NormalFillDark { get; private set; }
        internal Color NormalFillLight { get; private set; }
        internal Color FlowFillDark { get; private set; }
        internal Color FlowFillLight { get; private set; }
        internal Color BranchFillDark { get; private set; }
        internal Color BranchFillLight { get; private set; }
        internal Color ServiceFillDark { get; private set; }
        internal Color ServiceFillLight { get; private set; }
        internal Color ConditionFillDark { get; private set; }
        internal Color ConditionFillLight { get; private set; }
        internal Color CompoundFillDark { get; private set; }
        internal Color CompoundFillLight { get; private set; }
        internal Color NormalStrokeDark { get; private set; }
        internal Color NormalStrokeLight { get; private set; }
        internal Color FlowStroke { get; private set; }
        internal Color BranchStroke { get; private set; }
        internal Color ServiceStroke { get; private set; }
        internal Color SelectedStroke { get; private set; }
        internal Color WarningStroke { get; private set; }
        internal Color StructuralEdge { get; private set; }
        internal Color ServiceEdge { get; private set; }
        internal Color RawEdge { get; private set; }
        internal Color FlowEdge { get; private set; }
        internal Color BranchEdge { get; private set; }
        internal Color ProbabilityEdge { get; private set; }
        internal Color ParallelEdge { get; private set; }
        internal Color LoopEdge { get; private set; }
        internal Color SequenceScope { get; private set; }
        internal Color SequenceScopeSelected { get; private set; }
        internal Color ConditionScope { get; private set; }
        internal Color ConditionScopeSelected { get; private set; }
        internal Color ProbabilityScope { get; private set; }
        internal Color ProbabilityScopeSelected { get; private set; }
        internal float GridLineWidth { get; private set; }
        internal float NodeLineWidth { get; private set; }
        internal float SelectedLineWidth { get; private set; }
        internal float ScopeLineWidth { get; private set; }
        internal float SelectedScopeLineWidth { get; private set; }
        internal float AuthoredLineWidth { get; private set; }
        internal float DerivedLineWidth { get; private set; }
        internal float PlaceholderLineWidth { get; private set; }
        internal float DerivedMarkLength { get; private set; }
        internal float DerivedGapLength { get; private set; }
        internal float ControlMarkLength { get; private set; }
        internal float ControlGapLength { get; private set; }
        internal float PlaceholderMarkLength { get; private set; }
        internal float PlaceholderGapLength { get; private set; }
        internal float DisabledAlpha { get; private set; }
        internal bool HasResolvedCustomStyles { get; private set; }

        /// <summary>Resolves all directly assigned graph custom properties, falling back per property.</summary>
        internal void Resolve(ICustomStyle customStyle)
        {
            ResetToDefaults();
            if (customStyle == null)
            {
                return;
            }

            HasResolvedCustomStyles = customStyle.TryGetValue(GridDarkProperty, out Color _);
            GridDark = Get(customStyle, GridDarkProperty, GridDark);
            GridLight = Get(customStyle, GridLightProperty, GridLight);
            NormalFillDark = Get(customStyle, NormalFillDarkProperty, NormalFillDark);
            NormalFillLight = Get(customStyle, NormalFillLightProperty, NormalFillLight);
            FlowFillDark = Get(customStyle, FlowFillDarkProperty, FlowFillDark);
            FlowFillLight = Get(customStyle, FlowFillLightProperty, FlowFillLight);
            BranchFillDark = Get(customStyle, BranchFillDarkProperty, BranchFillDark);
            BranchFillLight = Get(customStyle, BranchFillLightProperty, BranchFillLight);
            ServiceFillDark = Get(customStyle, ServiceFillDarkProperty, ServiceFillDark);
            ServiceFillLight = Get(customStyle, ServiceFillLightProperty, ServiceFillLight);
            ConditionFillDark = Get(customStyle, ConditionFillDarkProperty, ConditionFillDark);
            ConditionFillLight = Get(customStyle, ConditionFillLightProperty, ConditionFillLight);
            CompoundFillDark = Get(customStyle, CompoundFillDarkProperty, CompoundFillDark);
            CompoundFillLight = Get(customStyle, CompoundFillLightProperty, CompoundFillLight);
            NormalStrokeDark = Get(customStyle, NormalStrokeDarkProperty, NormalStrokeDark);
            NormalStrokeLight = Get(customStyle, NormalStrokeLightProperty, NormalStrokeLight);
            FlowStroke = Get(customStyle, FlowStrokeProperty, FlowStroke);
            BranchStroke = Get(customStyle, BranchStrokeProperty, BranchStroke);
            ServiceStroke = Get(customStyle, ServiceStrokeProperty, ServiceStroke);
            SelectedStroke = Get(customStyle, SelectedStrokeProperty, SelectedStroke);
            WarningStroke = Get(customStyle, WarningStrokeProperty, WarningStroke);
            StructuralEdge = Get(customStyle, StructuralEdgeProperty, StructuralEdge);
            ServiceEdge = Get(customStyle, ServiceEdgeProperty, ServiceEdge);
            RawEdge = Get(customStyle, RawEdgeProperty, RawEdge);
            FlowEdge = Get(customStyle, FlowEdgeProperty, FlowEdge);
            BranchEdge = Get(customStyle, BranchEdgeProperty, BranchEdge);
            ProbabilityEdge = Get(customStyle, ProbabilityEdgeProperty, ProbabilityEdge);
            ParallelEdge = Get(customStyle, ParallelEdgeProperty, ParallelEdge);
            LoopEdge = Get(customStyle, LoopEdgeProperty, LoopEdge);
            SequenceScope = Get(customStyle, SequenceScopeProperty, SequenceScope);
            SequenceScopeSelected = Get(customStyle, SequenceScopeSelectedProperty, SequenceScopeSelected);
            ConditionScope = Get(customStyle, ConditionScopeProperty, ConditionScope);
            ConditionScopeSelected = Get(customStyle, ConditionScopeSelectedProperty, ConditionScopeSelected);
            ProbabilityScope = Get(customStyle, ProbabilityScopeProperty, ProbabilityScope);
            ProbabilityScopeSelected = Get(customStyle, ProbabilityScopeSelectedProperty, ProbabilityScopeSelected);
            GridLineWidth = GetPositive(customStyle, GridLineWidthProperty, GridLineWidth);
            NodeLineWidth = GetPositive(customStyle, NodeLineWidthProperty, NodeLineWidth);
            SelectedLineWidth = GetPositive(customStyle, SelectedLineWidthProperty, SelectedLineWidth);
            ScopeLineWidth = GetPositive(customStyle, ScopeLineWidthProperty, ScopeLineWidth);
            SelectedScopeLineWidth = GetPositive(customStyle, SelectedScopeLineWidthProperty, SelectedScopeLineWidth);
            AuthoredLineWidth = GetPositive(customStyle, AuthoredLineWidthProperty, AuthoredLineWidth);
            DerivedLineWidth = GetPositive(customStyle, DerivedLineWidthProperty, DerivedLineWidth);
            PlaceholderLineWidth = GetPositive(customStyle, PlaceholderLineWidthProperty, PlaceholderLineWidth);
            DerivedMarkLength = GetPositive(customStyle, DerivedMarkLengthProperty, DerivedMarkLength);
            DerivedGapLength = GetPositive(customStyle, DerivedGapLengthProperty, DerivedGapLength);
            ControlMarkLength = GetPositive(customStyle, ControlMarkLengthProperty, ControlMarkLength);
            ControlGapLength = GetPositive(customStyle, ControlGapLengthProperty, ControlGapLength);
            PlaceholderMarkLength = GetPositive(customStyle, PlaceholderMarkLengthProperty, PlaceholderMarkLength);
            PlaceholderGapLength = GetPositive(customStyle, PlaceholderGapLengthProperty, PlaceholderGapLength);
            if (customStyle.TryGetValue(DisabledAlphaProperty, out float disabledAlpha))
            {
                DisabledAlpha = Mathf.Clamp01(disabledAlpha);
            }
        }

        /// <summary>Restores the exact paint values used before USS customization.</summary>
        internal void ResetToDefaults()
        {
            GridDark = new Color(1f, 1f, 1f, 0.045f);
            GridLight = new Color(0f, 0f, 0f, 0.055f);
            NormalFillDark = new Color(0.16f, 0.17f, 0.19f, 0.98f);
            NormalFillLight = new Color(0.82f, 0.83f, 0.85f, 0.98f);
            FlowFillDark = new Color(0.12f, 0.24f, 0.31f, 0.98f);
            FlowFillLight = new Color(0.72f, 0.86f, 0.91f, 0.98f);
            BranchFillDark = new Color(0.25f, 0.18f, 0.31f, 0.98f);
            BranchFillLight = new Color(0.85f, 0.78f, 0.91f, 0.98f);
            ServiceFillDark = new Color(0.30f, 0.23f, 0.10f, 0.98f);
            ServiceFillLight = new Color(0.93f, 0.86f, 0.68f, 0.98f);
            ConditionFillDark = new Color(0.12f, 0.10f, 0.16f, 0.7f);
            ConditionFillLight = new Color(0.88f, 0.84f, 0.92f, 0.7f);
            CompoundFillDark = new Color(0.10f, 0.12f, 0.15f, 0.96f);
            CompoundFillLight = new Color(0.88f, 0.90f, 0.93f, 0.96f);
            NormalStrokeDark = new Color(0.62f, 0.65f, 0.7f, 0.9f);
            NormalStrokeLight = new Color(0.32f, 0.35f, 0.4f, 0.9f);
            FlowStroke = new Color(0.25f, 0.67f, 0.82f, 0.95f);
            BranchStroke = new Color(0.68f, 0.45f, 0.86f, 0.95f);
            ServiceStroke = new Color(0.91f, 0.66f, 0.21f, 0.95f);
            SelectedStroke = new Color(0.25f, 0.62f, 1f, 1f);
            WarningStroke = new Color(1f, 0.48f, 0.25f, 0.95f);
            StructuralEdge = new Color(0.72f, 0.72f, 0.72f, 1f);
            ServiceEdge = new Color(0.95f, 0.72f, 0.25f, 1f);
            RawEdge = new Color(0.55f, 0.65f, 0.9f, 1f);
            FlowEdge = new Color(0.25f, 0.72f, 0.92f, 1f);
            BranchEdge = new Color(0.72f, 0.48f, 0.92f, 1f);
            ProbabilityEdge = new Color(0.95f, 0.72f, 0.25f, 1f);
            ParallelEdge = new Color(0.35f, 0.66f, 0.95f, 1f);
            LoopEdge = new Color(0.28f, 0.82f, 0.72f, 1f);
            SequenceScope = new Color(0.25f, 0.72f, 0.92f, 0.42f);
            SequenceScopeSelected = new Color(0.25f, 0.62f, 1f, 0.9f);
            ConditionScope = new Color(0.72f, 0.48f, 0.92f, 0.38f);
            ConditionScopeSelected = new Color(0.72f, 0.48f, 0.92f, 0.95f);
            ProbabilityScope = new Color(0.95f, 0.72f, 0.25f, 0.32f);
            ProbabilityScopeSelected = new Color(0.95f, 0.72f, 0.25f, 0.9f);
            GridLineWidth = 1f;
            NodeLineWidth = 1.5f;
            SelectedLineWidth = 2.5f;
            ScopeLineWidth = 1.25f;
            SelectedScopeLineWidth = 2f;
            AuthoredLineWidth = 2f;
            DerivedLineWidth = 1.25f;
            PlaceholderLineWidth = 1f;
            DerivedMarkLength = 8f;
            DerivedGapLength = 5f;
            ControlMarkLength = 4f;
            ControlGapLength = 4f;
            PlaceholderMarkLength = 2f;
            PlaceholderGapLength = 6f;
            DisabledAlpha = 0.32f;
            HasResolvedCustomStyles = false;
        }

        private static Color Get(ICustomStyle customStyle, CustomStyleProperty<Color> property, Color fallback)
        {
            return customStyle.TryGetValue(property, out Color value) ? value : fallback;
        }

        private static float GetPositive(ICustomStyle customStyle, CustomStyleProperty<float> property, float fallback)
        {
            return customStyle.TryGetValue(property, out float value) && value > 0f ? value : fallback;
        }
    }
}
