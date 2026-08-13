using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Defines the shared unscaled geometry used by graph presentation, layout, and rendering.
    /// </summary>
    internal static class GraphPresentationMetrics
    {
        internal static readonly Vector2 NormalNodeSize = new(168f, 40f);
        internal static readonly Vector2 FlowNodeSize = new(188f, 52f);
        internal static readonly Vector2 BranchNodeSize = new(176f, 52f);
        internal static readonly Vector2 ServiceNodeSize = new(152f, 40f);
        internal static readonly Vector2 DecoratorNodeSize = new(112f, 28f);
        internal static readonly Vector2 BooleanNodeSize = new(112f, 26f);
        internal static readonly Vector2 ConstantNodeSize = new(64f, 24f);
        internal static readonly Vector2 ReferenceItemSize = new(180f, 48f);
        internal static readonly Vector2 ConditionPlaceholderSize = new(160f, 46f);
        internal static readonly Vector2 LoopPlaceholderSize = new(160f, 46f);
        internal static readonly Vector2 LoopCountCheckSize = new(160f, 42f);
        internal static readonly Vector2 ServicePlaceholderSize = new(152f, 42f);
        internal static readonly Vector2 ProbabilityPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 DecisionPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ParallelPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ForEachPlaceholderSize = new(176f, 48f);
        internal static readonly Vector2 ForEachCheckSize = new(164f, 42f);

        internal const float FlowCompletionMinimumWidth = 96f;
        internal const float FlowCompletionMaximumWidth = 220f;
        internal const float FlowCompletionHeight = 24f;

        internal const float SiblingGap = 32f;
        internal const float LevelGap = 36f;
        internal const float ServiceGap = 20f;
        internal const float ServiceScopePadding = 12f;
        internal const float ServiceScopeHeader = 22f;
        internal const float UnreachableGap = 44f;
        internal const float ConditionPadding = 8f;
        internal const float ConditionHeader = 24f;
        internal const float ConditionMinimumWidth = 168f;
        internal const float ConditionBranchGap = 48f;
        internal const float ConditionBranchLevelGap = 48f;
        internal const float ConditionBracketOffset = 14f;
        internal const float ProbabilityBranchGap = 48f;
        internal const float ProbabilityBranchLevelGap = 48f;
        internal const float ProbabilityFanOffset = 14f;
        internal const float DecisionBranchGap = 48f;
        internal const float DecisionBranchLevelGap = 48f;
        internal const float FlowCompletionGap = 30f;
        internal const float SequenceRailOffset = 18f;
        internal const float LoopBodyFramePadding = 14f;
        internal const float LoopBodyFrameHeader = 20f;
        internal const float LoopReturnRailGap = 18f;
        internal const float LoopExitRailGap = 18f;
        internal const float ParallelForkGap = 22f;
        internal const float ParallelJoinGap = 28f;
        internal const float ForEachBodyFramePadding = 14f;
        internal const float ForEachBodyFrameHeader = 20f;

        /// <summary>
        /// Returns a deterministic completion marker size without depending on resolved panel geometry.
        /// </summary>
        /// <param name="displayName">The owning Flow display name.</param>
        /// <returns>The unscaled presentation size reserved for the completion marker.</returns>
        internal static Vector2 GetFlowCompletionSize(string displayName)
        {
            const float fixedTextAndPaddingWidth = 54f;
            float estimatedNameWidth = 0f;
            foreach (char character in displayName ?? string.Empty)
            {
                estimatedNameWidth += char.IsWhiteSpace(character) ? 3.5f : character <= 0x7f ? 5.5f : 9f;
            }

            return new Vector2(
                Mathf.Clamp(
                    fixedTextAndPaddingWidth + estimatedNameWidth,
                    FlowCompletionMinimumWidth,
                    FlowCompletionMaximumWidth),
                FlowCompletionHeight);
        }
    }

    /// <summary>
    /// Editor-only semantic presentation role for a graph item.
    /// </summary>
}
