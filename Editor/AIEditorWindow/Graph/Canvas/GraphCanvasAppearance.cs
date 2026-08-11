using Aethiumian.AI.Nodes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>Canvas-only color families for composite Graph owners.</summary>
    internal enum GraphVisualFamily
    {
        Neutral,
        Sequence,
        Loop,
        Condition,
        Decision,
        Probability,
        Parallel,
        Service,
    }

    /// <summary>
    /// Holds USS-resolved paint values for one graph canvas without owning layout or topology state.
    /// </summary>
    internal sealed class GraphCanvasAppearance
    {
        private static readonly CustomStyleProperty<Color> GridDarkProperty = new("--graph-grid-dark");
        private static readonly CustomStyleProperty<Color> GridLightProperty = new("--graph-grid-light");
        private static readonly CustomStyleProperty<Color> NormalFillDarkProperty = new("--graph-node-normal-fill-dark");
        private static readonly CustomStyleProperty<Color> NormalFillLightProperty = new("--graph-node-normal-fill-light");
        private static readonly CustomStyleProperty<Color> BooleanFillDarkProperty = new("--graph-leaf-boolean-fill-dark");
        private static readonly CustomStyleProperty<Color> BooleanFillLightProperty = new("--graph-leaf-boolean-fill-light");
        private static readonly CustomStyleProperty<Color> BooleanStrokeProperty = new("--graph-leaf-boolean-stroke");
        private static readonly CustomStyleProperty<Color> ConstantTrueFillDarkProperty = new("--graph-leaf-constant-true-fill-dark");
        private static readonly CustomStyleProperty<Color> ConstantTrueFillLightProperty = new("--graph-leaf-constant-true-fill-light");
        private static readonly CustomStyleProperty<Color> ConstantTrueStrokeProperty = new("--graph-leaf-constant-true-stroke");
        private static readonly CustomStyleProperty<Color> ConstantFalseFillDarkProperty = new("--graph-leaf-constant-false-fill-dark");
        private static readonly CustomStyleProperty<Color> ConstantFalseFillLightProperty = new("--graph-leaf-constant-false-fill-light");
        private static readonly CustomStyleProperty<Color> ConstantFalseStrokeProperty = new("--graph-leaf-constant-false-stroke");
        private static readonly CustomStyleProperty<Color> FlowFillDarkProperty = new("--graph-node-flow-fill-dark");
        private static readonly CustomStyleProperty<Color> FlowFillLightProperty = new("--graph-node-flow-fill-light");
        private static readonly CustomStyleProperty<Color> BranchFillDarkProperty = new("--graph-node-branch-fill-dark");
        private static readonly CustomStyleProperty<Color> BranchFillLightProperty = new("--graph-node-branch-fill-light");
        private static readonly CustomStyleProperty<Color> ServiceFillDarkProperty = new("--graph-node-service-fill-dark");
        private static readonly CustomStyleProperty<Color> ServiceFillLightProperty = new("--graph-node-service-fill-light");
        private static readonly CustomStyleProperty<Color> ConditionFillDarkProperty = new("--graph-condition-fill-dark");
        private static readonly CustomStyleProperty<Color> ConditionFillLightProperty = new("--graph-condition-fill-light");
        private static readonly CustomStyleProperty<Color> SequenceFamilyFillDarkProperty = new("--graph-family-sequence-fill-dark");
        private static readonly CustomStyleProperty<Color> SequenceFamilyFillLightProperty = new("--graph-family-sequence-fill-light");
        private static readonly CustomStyleProperty<Color> LoopFamilyFillDarkProperty = new("--graph-family-loop-fill-dark");
        private static readonly CustomStyleProperty<Color> LoopFamilyFillLightProperty = new("--graph-family-loop-fill-light");
        private static readonly CustomStyleProperty<Color> ConditionFamilyFillDarkProperty = new("--graph-family-condition-fill-dark");
        private static readonly CustomStyleProperty<Color> ConditionFamilyFillLightProperty = new("--graph-family-condition-fill-light");
        private static readonly CustomStyleProperty<Color> DecisionFamilyFillDarkProperty = new("--graph-family-decision-fill-dark");
        private static readonly CustomStyleProperty<Color> DecisionFamilyFillLightProperty = new("--graph-family-decision-fill-light");
        private static readonly CustomStyleProperty<Color> ProbabilityFamilyFillDarkProperty = new("--graph-family-probability-fill-dark");
        private static readonly CustomStyleProperty<Color> ProbabilityFamilyFillLightProperty = new("--graph-family-probability-fill-light");
        private static readonly CustomStyleProperty<Color> ParallelFamilyFillDarkProperty = new("--graph-family-parallel-fill-dark");
        private static readonly CustomStyleProperty<Color> ParallelFamilyFillLightProperty = new("--graph-family-parallel-fill-light");
        private static readonly CustomStyleProperty<Color> SequenceFamilyStrokeProperty = new("--graph-family-sequence-stroke");
        private static readonly CustomStyleProperty<Color> LoopFamilyStrokeProperty = new("--graph-family-loop-stroke");
        private static readonly CustomStyleProperty<Color> ConditionFamilyStrokeProperty = new("--graph-family-condition-stroke");
        private static readonly CustomStyleProperty<Color> DecisionFamilyStrokeProperty = new("--graph-family-decision-stroke");
        private static readonly CustomStyleProperty<Color> ProbabilityFamilyStrokeProperty = new("--graph-family-probability-stroke");
        private static readonly CustomStyleProperty<Color> ParallelFamilyStrokeProperty = new("--graph-family-parallel-stroke");
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
        internal Color BooleanFillDark { get; private set; }
        internal Color BooleanFillLight { get; private set; }
        internal Color BooleanStroke { get; private set; }
        internal Color ConstantTrueFillDark { get; private set; }
        internal Color ConstantTrueFillLight { get; private set; }
        internal Color ConstantTrueStroke { get; private set; }
        internal Color ConstantFalseFillDark { get; private set; }
        internal Color ConstantFalseFillLight { get; private set; }
        internal Color ConstantFalseStroke { get; private set; }
        internal Color FlowFillDark { get; private set; }
        internal Color FlowFillLight { get; private set; }
        internal Color BranchFillDark { get; private set; }
        internal Color BranchFillLight { get; private set; }
        internal Color ServiceFillDark { get; private set; }
        internal Color ServiceFillLight { get; private set; }
        internal Color ConditionFillDark { get; private set; }
        internal Color ConditionFillLight { get; private set; }
        internal Color SequenceFamilyFillDark { get; private set; }
        internal Color SequenceFamilyFillLight { get; private set; }
        internal Color LoopFamilyFillDark { get; private set; }
        internal Color LoopFamilyFillLight { get; private set; }
        internal Color ConditionFamilyFillDark { get; private set; }
        internal Color ConditionFamilyFillLight { get; private set; }
        internal Color DecisionFamilyFillDark { get; private set; }
        internal Color DecisionFamilyFillLight { get; private set; }
        internal Color ProbabilityFamilyFillDark { get; private set; }
        internal Color ProbabilityFamilyFillLight { get; private set; }
        internal Color ParallelFamilyFillDark { get; private set; }
        internal Color ParallelFamilyFillLight { get; private set; }
        internal Color SequenceFamilyStroke { get; private set; }
        internal Color LoopFamilyStroke { get; private set; }
        internal Color ConditionFamilyStroke { get; private set; }
        internal Color DecisionFamilyStroke { get; private set; }
        internal Color ProbabilityFamilyStroke { get; private set; }
        internal Color ParallelFamilyStroke { get; private set; }
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

        /// <summary>Classifies a node without altering its topology shape or presentation contract.</summary>
        internal static GraphVisualFamily GetFamily(TreeNode node)
        {
            return node switch
            {
                Sequence => GraphVisualFamily.Sequence,
                Loop or ForEach => GraphVisualFamily.Loop,
                Condition => GraphVisualFamily.Condition,
                Decision => GraphVisualFamily.Decision,
                Probability or PseudoProbability => GraphVisualFamily.Probability,
                Parallel => GraphVisualFamily.Parallel,
                Service => GraphVisualFamily.Service,
                _ => GraphVisualFamily.Neutral,
            };
        }

        /// <summary>Gets the family stroke used by cards, authored edges, and derived flow chrome.</summary>
        internal Color GetFamilyStroke(GraphVisualFamily family)
        {
            return family switch
            {
                GraphVisualFamily.Sequence => SequenceFamilyStroke,
                GraphVisualFamily.Loop => LoopFamilyStroke,
                GraphVisualFamily.Condition => ConditionFamilyStroke,
                GraphVisualFamily.Decision => DecisionFamilyStroke,
                GraphVisualFamily.Probability => ProbabilityFamilyStroke,
                GraphVisualFamily.Parallel => ParallelFamilyStroke,
                GraphVisualFamily.Service => ServiceStroke,
                _ => EditorGUIUtility.isProSkin ? NormalStrokeDark : NormalStrokeLight,
            };
        }

        /// <summary>Gets the theme-aware family card fill while retaining the source family's hue.</summary>
        internal Color GetFamilyFill(GraphVisualFamily family, bool proSkin)
        {
            return family switch
            {
                GraphVisualFamily.Sequence => proSkin ? SequenceFamilyFillDark : SequenceFamilyFillLight,
                GraphVisualFamily.Loop => proSkin ? LoopFamilyFillDark : LoopFamilyFillLight,
                GraphVisualFamily.Condition => proSkin ? ConditionFamilyFillDark : ConditionFamilyFillLight,
                GraphVisualFamily.Decision => proSkin ? DecisionFamilyFillDark : DecisionFamilyFillLight,
                GraphVisualFamily.Probability => proSkin ? ProbabilityFamilyFillDark : ProbabilityFamilyFillLight,
                GraphVisualFamily.Parallel => proSkin ? ParallelFamilyFillDark : ParallelFamilyFillLight,
                GraphVisualFamily.Service => proSkin ? ServiceFillDark : ServiceFillLight,
                _ => proSkin ? NormalFillDark : NormalFillLight,
            };
        }

        /// <summary>Gets the exact family color for a relation, preferring its flow owner for derived chrome.</summary>
        internal Color GetRelationColor(GraphPresentationRelation relation)
        {
            if (relation == null)
            {
                return StructuralEdge;
            }

            GraphPresentationItem owner = relation.VisualOwner ?? (relation.Role == GraphPresentationRelationRole.AuthoredReference
                ? relation.Source.Item
                : relation.ContextualOwner ?? (relation.Target.Anchor == GraphPresentationAnchorKind.FlowComplete ? relation.Target.Item : null));
            return owner?.Node?.Node != null
                ? GetFamilyStroke(GetFamily(owner.Node.Node))
                : StructuralEdge;
        }

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
            BooleanFillDark = Get(customStyle, BooleanFillDarkProperty, BooleanFillDark);
            BooleanFillLight = Get(customStyle, BooleanFillLightProperty, BooleanFillLight);
            BooleanStroke = Get(customStyle, BooleanStrokeProperty, BooleanStroke);
            ConstantTrueFillDark = Get(customStyle, ConstantTrueFillDarkProperty, ConstantTrueFillDark);
            ConstantTrueFillLight = Get(customStyle, ConstantTrueFillLightProperty, ConstantTrueFillLight);
            ConstantTrueStroke = Get(customStyle, ConstantTrueStrokeProperty, ConstantTrueStroke);
            ConstantFalseFillDark = Get(customStyle, ConstantFalseFillDarkProperty, ConstantFalseFillDark);
            ConstantFalseFillLight = Get(customStyle, ConstantFalseFillLightProperty, ConstantFalseFillLight);
            ConstantFalseStroke = Get(customStyle, ConstantFalseStrokeProperty, ConstantFalseStroke);
            FlowFillDark = Get(customStyle, FlowFillDarkProperty, FlowFillDark);
            FlowFillLight = Get(customStyle, FlowFillLightProperty, FlowFillLight);
            BranchFillDark = Get(customStyle, BranchFillDarkProperty, BranchFillDark);
            BranchFillLight = Get(customStyle, BranchFillLightProperty, BranchFillLight);
            ServiceFillDark = Get(customStyle, ServiceFillDarkProperty, ServiceFillDark);
            ServiceFillLight = Get(customStyle, ServiceFillLightProperty, ServiceFillLight);
            ConditionFillDark = Get(customStyle, ConditionFillDarkProperty, ConditionFillDark);
            ConditionFillLight = Get(customStyle, ConditionFillLightProperty, ConditionFillLight);
            SequenceFamilyFillDark = Get(customStyle, SequenceFamilyFillDarkProperty, SequenceFamilyFillDark);
            SequenceFamilyFillLight = Get(customStyle, SequenceFamilyFillLightProperty, SequenceFamilyFillLight);
            LoopFamilyFillDark = Get(customStyle, LoopFamilyFillDarkProperty, LoopFamilyFillDark);
            LoopFamilyFillLight = Get(customStyle, LoopFamilyFillLightProperty, LoopFamilyFillLight);
            ConditionFamilyFillDark = Get(customStyle, ConditionFamilyFillDarkProperty, ConditionFamilyFillDark);
            ConditionFamilyFillLight = Get(customStyle, ConditionFamilyFillLightProperty, ConditionFamilyFillLight);
            DecisionFamilyFillDark = Get(customStyle, DecisionFamilyFillDarkProperty, DecisionFamilyFillDark);
            DecisionFamilyFillLight = Get(customStyle, DecisionFamilyFillLightProperty, DecisionFamilyFillLight);
            ProbabilityFamilyFillDark = Get(customStyle, ProbabilityFamilyFillDarkProperty, ProbabilityFamilyFillDark);
            ProbabilityFamilyFillLight = Get(customStyle, ProbabilityFamilyFillLightProperty, ProbabilityFamilyFillLight);
            ParallelFamilyFillDark = Get(customStyle, ParallelFamilyFillDarkProperty, ParallelFamilyFillDark);
            ParallelFamilyFillLight = Get(customStyle, ParallelFamilyFillLightProperty, ParallelFamilyFillLight);
            SequenceFamilyStroke = Get(customStyle, SequenceFamilyStrokeProperty, SequenceFamilyStroke);
            LoopFamilyStroke = Get(customStyle, LoopFamilyStrokeProperty, LoopFamilyStroke);
            ConditionFamilyStroke = Get(customStyle, ConditionFamilyStrokeProperty, ConditionFamilyStroke);
            DecisionFamilyStroke = Get(customStyle, DecisionFamilyStrokeProperty, DecisionFamilyStroke);
            ProbabilityFamilyStroke = Get(customStyle, ProbabilityFamilyStrokeProperty, ProbabilityFamilyStroke);
            ParallelFamilyStroke = Get(customStyle, ParallelFamilyStrokeProperty, ParallelFamilyStroke);
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
            BooleanFillDark = new Color(0.22f, 0.22f, 0.46f, 0.98f);
            BooleanFillLight = new Color(0.68f, 0.70f, 0.95f, 0.98f);
            BooleanStroke = new Color(0.49f, 0.52f, 0.96f, 1f);
            ConstantTrueFillDark = new Color(0.16f, 0.40f, 0.24f, 0.98f);
            ConstantTrueFillLight = new Color(0.62f, 0.88f, 0.68f, 0.98f);
            ConstantTrueStroke = new Color(0.38f, 0.86f, 0.52f, 1f);
            ConstantFalseFillDark = new Color(0.44f, 0.18f, 0.20f, 0.98f);
            ConstantFalseFillLight = new Color(0.94f, 0.66f, 0.68f, 0.98f);
            ConstantFalseStroke = new Color(0.94f, 0.40f, 0.42f, 1f);
            FlowFillDark = new Color(0.12f, 0.24f, 0.31f, 0.98f);
            FlowFillLight = new Color(0.72f, 0.86f, 0.91f, 0.98f);
            BranchFillDark = new Color(0.25f, 0.18f, 0.31f, 0.98f);
            BranchFillLight = new Color(0.85f, 0.78f, 0.91f, 0.98f);
            ServiceFillDark = new Color(0.30f, 0.23f, 0.10f, 0.98f);
            ServiceFillLight = new Color(0.93f, 0.86f, 0.68f, 0.98f);
            ConditionFillDark = new Color(0.12f, 0.10f, 0.16f, 0.7f);
            ConditionFillLight = new Color(0.88f, 0.84f, 0.92f, 0.7f);
            SequenceFamilyFillDark = new Color(64f / 255f, 184f / 255f, 235f / 255f, 0.22f);
            SequenceFamilyFillLight = new Color(64f / 255f, 184f / 255f, 235f / 255f, 0.14f);
            LoopFamilyFillDark = new Color(71f / 255f, 209f / 255f, 184f / 255f, 0.22f);
            LoopFamilyFillLight = new Color(71f / 255f, 209f / 255f, 184f / 255f, 0.14f);
            ConditionFamilyFillDark = new Color(184f / 255f, 122f / 255f, 235f / 255f, 0.12f);
            ConditionFamilyFillLight = new Color(184f / 255f, 122f / 255f, 235f / 255f, 0.08f);
            DecisionFamilyFillDark = new Color(126f / 255f, 138f / 255f, 242f / 255f, 0.22f);
            DecisionFamilyFillLight = new Color(126f / 255f, 138f / 255f, 242f / 255f, 0.14f);
            ProbabilityFamilyFillDark = new Color(232f / 255f, 111f / 255f, 154f / 255f, 0.22f);
            ProbabilityFamilyFillLight = new Color(232f / 255f, 111f / 255f, 154f / 255f, 0.14f);
            ParallelFamilyFillDark = new Color(89f / 255f, 168f / 255f, 242f / 255f, 0.22f);
            ParallelFamilyFillLight = new Color(89f / 255f, 168f / 255f, 242f / 255f, 0.14f);
            SequenceFamilyStroke = new Color(64f / 255f, 184f / 255f, 235f / 255f, 1f);
            LoopFamilyStroke = new Color(71f / 255f, 209f / 255f, 184f / 255f, 1f);
            ConditionFamilyStroke = new Color(184f / 255f, 122f / 255f, 235f / 255f, 1f);
            DecisionFamilyStroke = new Color(126f / 255f, 138f / 255f, 242f / 255f, 1f);
            ProbabilityFamilyStroke = new Color(232f / 255f, 111f / 255f, 154f / 255f, 1f);
            ParallelFamilyStroke = new Color(89f / 255f, 168f / 255f, 242f / 255f, 1f);
            CompoundFillDark = new Color(0.10f, 0.12f, 0.15f, 0.96f);
            CompoundFillLight = new Color(0.88f, 0.90f, 0.93f, 0.96f);
            NormalStrokeDark = new Color(0.62f, 0.65f, 0.7f, 0.9f);
            NormalStrokeLight = new Color(0.32f, 0.35f, 0.4f, 0.9f);
            FlowStroke = new Color(64f / 255f, 184f / 255f, 235f / 255f, 0.95f);
            BranchStroke = new Color(0.68f, 0.45f, 0.86f, 0.95f);
            ServiceStroke = new Color(0.91f, 0.66f, 0.21f, 0.95f);
            SelectedStroke = new Color(0.25f, 0.62f, 1f, 1f);
            WarningStroke = new Color(1f, 0.48f, 0.25f, 0.95f);
            StructuralEdge = new Color(0.72f, 0.72f, 0.72f, 1f);
            ServiceEdge = new Color(0.95f, 0.72f, 0.25f, 1f);
            RawEdge = new Color(0.55f, 0.65f, 0.9f, 1f);
            FlowEdge = new Color(0.25f, 0.72f, 0.92f, 1f);
            BranchEdge = new Color(0.72f, 0.48f, 0.92f, 1f);
            ProbabilityEdge = new Color(232f / 255f, 111f / 255f, 154f / 255f, 1f);
            ParallelEdge = new Color(89f / 255f, 168f / 255f, 242f / 255f, 1f);
            LoopEdge = new Color(71f / 255f, 209f / 255f, 184f / 255f, 1f);
            SequenceScope = new Color(0.25f, 0.72f, 0.92f, 0.42f);
            SequenceScopeSelected = new Color(0.25f, 0.62f, 1f, 0.9f);
            ConditionScope = new Color(0.72f, 0.48f, 0.92f, 0.38f);
            ConditionScopeSelected = new Color(0.72f, 0.48f, 0.92f, 0.95f);
            ProbabilityScope = new Color(232f / 255f, 111f / 255f, 154f / 255f, 0.32f);
            ProbabilityScopeSelected = new Color(232f / 255f, 111f / 255f, 154f / 255f, 0.9f);
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
