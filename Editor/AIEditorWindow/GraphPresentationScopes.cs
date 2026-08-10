using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal abstract class GraphFlowScope
    {
        private readonly List<GraphPresentationItem> members = new();

        protected GraphFlowScope(GraphPresentationItem owner)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        /// <summary>Gets the Flow presentation that owns this scope.</summary>
        internal GraphPresentationItem Owner { get; }

        /// <summary>Gets direct scope members in semantic order.</summary>
        internal IReadOnlyList<GraphPresentationItem> Members => members;

        /// <summary>Gets or sets the derived completion marker position.</summary>
        internal Vector2 CompletionPosition { get; set; }

        /// <summary>Gets the presentation size of this Flow completion marker.</summary>
        internal virtual Vector2 CompletionSize => GraphPresentationMetrics.GetFlowCompletionSize(
            Owner.Node?.DisplayName ?? "Flow");

        /// <summary>Gets or sets the derived scope bounds.</summary>
        internal Rect Bounds { get; set; }

        /// <summary>Adds one direct member to the scope.</summary>
        internal void AddMember(GraphPresentationItem member)
        {
            if (member != null)
            {
                members.Add(member);
            }
        }
    }

    /// <summary>
    /// Derived scope and rail for one free Sequence presentation.
    /// </summary>
    internal sealed class GraphSequenceScope : GraphFlowScope
    {
        internal GraphSequenceScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets or sets the derived bracket rail x coordinate.</summary>
        internal float RailX { get; set; }

        /// <summary>Gets or sets the derived bracket start y coordinate.</summary>
        internal float RailStartY { get; set; }

        /// <summary>Gets or sets the derived bracket end y coordinate.</summary>
        internal float RailEndY { get; set; }

    }

    /// <summary>
    /// Derived bracket and completion state for one free Condition presentation.
    /// </summary>
    internal sealed class GraphConditionScope : GraphFlowScope
    {
        internal GraphConditionScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets the True branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem TrueBranch { get; private set; }

        /// <summary>Gets the False branch item or its presentation-only placeholder.</summary>
        internal GraphPresentationItem FalseBranch { get; private set; }

        /// <summary>Gets or sets the left bracket rail coordinate.</summary>
        internal float LeftX { get; set; }

        /// <summary>Gets or sets the right bracket rail coordinate.</summary>
        internal float RightX { get; set; }

        /// <summary>Gets or sets the top coordinate of the branch bracket.</summary>
        internal float BracketTopY { get; set; }

        /// <summary>Gets or sets the bottom coordinate of the branch bracket.</summary>
        internal float BracketBottomY { get; set; }

        /// <summary>Assigns one branch lane and registers its item as a direct scope member.</summary>
        internal void SetBranch(GraphConditionBranch branch, GraphPresentationItem item)
        {
            if (branch == GraphConditionBranch.True)
            {
                TrueBranch = item;
            }
            else
            {
                FalseBranch = item;
            }

            AddMember(item);
        }
    }

    /// <summary>
    /// Derived fan and completion state for one freely arranged Probability family Flow.
    /// </summary>
    internal sealed class GraphProbabilityScope : GraphFlowScope
    {
        private readonly List<GraphProbabilityOption> options = new();

        internal GraphProbabilityScope(GraphPresentationItem owner, BehaviourTreeData tree) : base(owner)
        {
            Subtitle = BuildSubtitle(owner, tree);
        }

        /// <summary>Gets authored option occurrences in collection order.</summary>
        internal IReadOnlyList<GraphProbabilityOption> Options => options;

        /// <summary>Gets the concise card summary for the Probability mode.</summary>
        internal string Subtitle { get; }

        /// <summary>Gets or sets the left boundary of the derived candidate fan.</summary>
        internal float LeftX { get; set; }

        /// <summary>Gets or sets the right boundary of the derived candidate fan.</summary>
        internal float RightX { get; set; }

        /// <summary>Gets or sets the upper candidate fan coordinate.</summary>
        internal float FanTopY { get; set; }

        /// <summary>Gets or sets the lower candidate fan coordinate.</summary>
        internal float FanBottomY { get; set; }

        /// <summary>Adds one authored option occurrence as a direct scope member.</summary>
        internal void AddOption(GraphProbabilityOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            options.Add(option);
            AddMember(option.Item);
        }

        private static string BuildSubtitle(GraphPresentationItem owner, BehaviourTreeData tree)
        {
            if (owner.Node?.Node is not PseudoProbability pseudo)
            {
                return "PICK ONE";
            }

            VariableField<int> field = pseudo.maxConsecutiveBranch;
            if (field == null || field.IsConstant)
            {
                int value = field?.Constant ?? -1;
                return value > 0 ? $"PICK ONE · MAX STREAK {value}" : "PICK ONE · NO STREAK LIMIT";
            }

            string name = tree ? tree.GetVariableDescName(field.UUID) : VariableData.MISSING_VARIABLE_NAME;
            return $"PICK ONE · MAX STREAK {name}";
        }
    }

    /// <summary>Derived concurrent fork and synchronization join for one Parallel Flow.</summary>
    internal sealed class GraphParallelScope : GraphFlowScope
    {
        private readonly List<GraphPresentationItem> branches = new();

        internal GraphParallelScope(GraphPresentationItem owner) : base(owner)
        {
        }

        internal Parallel.Mode Mode => ((Parallel)Owner.Node.Node).mode;
        internal IReadOnlyList<GraphPresentationItem> Branches => branches;
        internal float ForkY { get; set; }
        internal float JoinY { get; set; }
        internal string JoinTitle => Mode == Parallel.Mode.WaitAll ? "WAIT ALL" : "FIRST COMPLETE";
        internal string JoinSubtitle => Mode == Parallel.Mode.WaitAll ? "All stacks stop" : "Stops remaining stacks";

        internal void AddBranch(GraphPresentationItem item)
        {
            if (item != null)
            {
                branches.Add(item);
                AddMember(item);
            }
        }
    }

    /// <summary>Derived enumerable check, free Body frame, and repeat path for one ForEach Flow.</summary>
    internal sealed class GraphForEachScope : GraphFlowScope
    {
        internal GraphForEachScope(GraphPresentationItem owner) : base(owner)
        {
        }

        internal GraphPresentationItem Check { get; private set; }
        internal GraphPresentationItem Body { get; private set; }
        internal GraphPresentationItem ItemOutputHint { get; private set; }
        internal Rect BodyFrameBounds { get; set; }
        internal float ReturnRailX => BodyFrameBounds.xMin - GraphPresentationMetrics.LoopReturnRailGap;

        internal void SetCheck(GraphPresentationItem item)
        {
            Check = item;
            AddMember(item);
        }

        internal void SetBody(GraphPresentationItem item)
        {
            Body = item;
            AddMember(item);
        }

        internal void SetItemOutputHint(GraphPresentationItem item)
        {
            ItemOutputHint = item;
            AddMember(item);
        }
    }

    /// <summary>
    /// Derived completion state for one Decision whose authored alternatives remain free branches.
    /// </summary>
    internal sealed class GraphDecisionScope : GraphFlowScope
    {
        private readonly List<GraphDecisionOption> options = new();

        internal GraphDecisionScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets authored alternatives in runtime attempt order.</summary>
        internal IReadOnlyList<GraphDecisionOption> Options => options;

        /// <summary>Gets or sets the shared success merge rail coordinate.</summary>
        internal float SuccessRailY { get; set; }

        /// <summary>Adds one authored alternative occurrence as a direct scope member.</summary>
        internal void AddOption(GraphDecisionOption option)
        {
            if (option == null)
            {
                throw new ArgumentNullException(nameof(option));
            }

            options.Add(option);
            AddMember(option.Item);
        }
    }

    /// <summary>
    /// Derived Body frame and exit state for one free Loop presentation.
    /// </summary>
    internal sealed class GraphLoopScope : GraphFlowScope
    {
        private readonly List<GraphPresentationItem> body = new();

        internal GraphLoopScope(GraphPresentationItem owner) : base(owner)
        {
        }

        /// <summary>Gets the authored Loop mode.</summary>
        internal Loop.LoopType Mode => ((Loop)Owner.Node.Node).loopType;

        /// <summary>Gets the condition card, placeholder, or derived count check.</summary>
        internal GraphPresentationItem Condition { get; private set; }

        /// <summary>Gets direct body occurrences in authored execution order.</summary>
        internal IReadOnlyList<GraphPresentationItem> Body => body;

        /// <summary>Gets or sets the lightweight frame derived from the complete Body envelope.</summary>
        internal Rect BodyFrameBounds { get; set; }

        /// <summary>Gets the derived repeat rail to the left of the Body frame.</summary>
        internal float ReturnRailX => BodyFrameBounds.xMin - GraphPresentationMetrics.LoopReturnRailGap;

        /// <summary>Gets the derived exit rail to the right of the Body frame.</summary>
        internal float ExitRailX => BodyFrameBounds.xMax + GraphPresentationMetrics.LoopExitRailGap;

        /// <summary>Assigns the condition or count-check item.</summary>
        internal void SetCondition(GraphPresentationItem item)
        {
            Condition = item;
            AddMember(item);
        }

        /// <summary>Adds one body occurrence in authored execution order.</summary>
        internal void AddBody(GraphPresentationItem item)
        {
            if (item == null)
            {
                return;
            }

            body.Add(item);
            AddMember(item);
        }

    }

    /// <summary>
    /// Derived, freely arranged boundary for one Service and its structural subtree.
    /// </summary>
    internal sealed class GraphServiceScope
    {
        private readonly List<GraphPresentationItem> members = new();

        internal GraphServiceScope(GraphPresentationItem owner, GraphPresentationItem host)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Gets the Service card that owns this unique scope.</summary>
        internal GraphPresentationItem Owner { get; }

        /// <summary>Gets the first-placement authored host.</summary>
        internal GraphPresentationItem Host { get; }

        /// <summary>Gets all real cards contained by this Service structural subtree.</summary>
        internal IReadOnlyList<GraphPresentationItem> Members => members;

        /// <summary>Gets or sets the derived frame bounds.</summary>
        internal Rect Bounds { get; set; }

        /// <summary>Gets or sets the number of additional authored hosts.</summary>
        internal int AdditionalHostCount { get; set; }

        /// <summary>Adds a unique member to this scope.</summary>
        internal void AddMember(GraphPresentationItem member)
        {
            if (member != null && !members.Contains(member))
            {
                members.Add(member);
            }
        }
    }

    /// <summary>
    /// A node presentation. Ordinary nodes are top-level free items; only a
    /// Condition may own an embedded predicate item.
    /// </summary>
}
