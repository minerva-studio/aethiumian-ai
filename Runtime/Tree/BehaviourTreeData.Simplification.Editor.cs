#if UNITY_EDITOR
using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Aethiumian.AI
{
    /// <summary>Editor-only result of a topology simplification request.</summary>
    internal readonly struct TopologySimplificationResult
    {
        internal TopologySimplificationResult(int applied, int removed)
        {
            AppliedRules = applied;
            RemovedNodes = removed;
        }

        internal int AppliedRules { get; }
        internal int RemovedNodes { get; }
        internal bool Changed => AppliedRules > 0;
    }

    public partial class BehaviourTreeData
    {
        private enum SimplificationKind
        {
            ReplaceWithChild,
            Flatten,
            ConditionTrueSequence,
            ConditionFalseDecision,
            InvertConditionPredicate,
            ConstantCondition,
            PruneSequence,
            PruneDecision,
            DoubleInverter,
        }

        /// <summary>Returns whether the supplied node has a safe transparent simplification.</summary>
        /// <param name="node">The candidate wrapper node.</param>
        /// <returns>True when the candidate can be reduced without changing authored values.</returns>
        internal bool CanSimplifyNode(TreeNode node)
        {
            return GetStructureValidationErrors().Count == 0
                && FindSimplifiable(node, NodeTopologySnapshot.Create(EditorNodes)) != null;
        }

        /// <summary>Explains why a node cannot currently be simplified.</summary>
        /// <param name="node">The node inspected by the Graph context menu.</param>
        /// <returns>A concise editor-facing reason.</returns>
        internal string GetSimplificationReason(TreeNode node)
        {
            IReadOnlyList<string> validationErrors = GetStructureValidationErrors();
            if (validationErrors.Count != 0)
            {
                return validationErrors[0];
            }
            if (node == null)
            {
                return "No node is selected.";
            }
            if (FindSimplifiable(node, NodeTopologySnapshot.Create(EditorNodes)) != null)
            {
                return string.Empty;
            }
            if (node is not Sequence && node is not Decision && node is not Condition
                && node is not Repeat && node is not Retry && node is not Always && node is not Inverter)
            {
                return "No topology simplification applies to this node type.";
            }
            if (node is ServiceHostNode host && host.services != null && host.services.Count != 0)
            {
                return "Service-hosting nodes are not moved or merged.";
            }
            if (NodeTopologySnapshot.Create(EditorNodes).GetRawIncomingCount(node) != 0)
            {
                return "The node has Raw incoming references.";
            }
            return "The required child or branch shape is not present.";
        }

        /// <summary>Simplifies one node-rooted chain in one undo transaction.</summary>
        /// <param name="node">The root candidate.</param>
        /// <param name="result">The applied-rule summary.</param>
        /// <returns>True when at least one node was removed.</returns>
        internal bool TrySimplifyNode(TreeNode node, out TopologySimplificationResult result)
        {
            result = default;
            if (node == null || GetStructureValidationErrors().Count != 0)
            {
                return false;
            }

            int undoGroup = BeginTransaction("Simplify AI topology", true);
            try
            {
                int applied = 0;
                int removed = 0;
                TreeNode current = node;
                while (true)
                {
                    SimplificationCandidate candidate = FindSimplifiable(current, NodeTopologySnapshot.Create(EditorNodes));
                    if (candidate == null || !ApplySimplification(candidate)) break;
                    applied++;
                    removed++;
                    current = candidate.Child;
                }

                ReconcileUnambiguousParents();
                if (GetStructureValidationErrors().Count != 0 || applied == 0)
                {
                    RollbackTransaction(undoGroup, new InvalidOperationException("Simplification produced invalid topology."));
                    return false;
                }

                CompleteTransaction(undoGroup);
                result = new TopologySimplificationResult(applied, removed);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        /// <summary>Simplifies all transparent wrappers in deterministic authored-node order.</summary>
        /// <param name="result">The applied-rule summary.</param>
        /// <returns>True when at least one node was removed.</returns>
        internal bool TrySimplifyAll(out TopologySimplificationResult result)
        {
            result = default;
            if (GetStructureValidationErrors().Count != 0) return false;
            int undoGroup = BeginTransaction("Simplify All AI topology", true);
            try
            {
                int applied = 0;
                int removed = 0;
                bool changed;
                do
                {
                    changed = false;
                    foreach (TreeNode candidateNode in nodes.ToArray())
                    {
                        SimplificationCandidate candidate = FindSimplifiable(candidateNode, NodeTopologySnapshot.Create(EditorNodes));
                        if (candidate == null || !ApplySimplification(candidate)) continue;
                        applied++;
                        removed++;
                        changed = true;
                    }
                } while (changed);

                ReconcileUnambiguousParents();
                if (applied == 0 || GetStructureValidationErrors().Count != 0)
                {
                    RollbackTransaction(undoGroup, new InvalidOperationException("Simplification produced invalid topology."));
                    return false;
                }

                CompleteTransaction(undoGroup);
                result = new TopologySimplificationResult(applied, removed);
                return true;
            }
            catch (Exception exception)
            {
                RollbackTransaction(undoGroup, exception);
                return false;
            }
        }

        private sealed class SimplificationCandidate
        {
            internal SimplificationKind Kind;
            internal TreeNode Wrapper;
            internal TreeNode Child;
            internal NodeReferenceOccurrence Incoming;
            internal TreeNode Flattened;
            internal TreeNode Predicate;
            internal TreeNode Selected;
            internal TreeNode Unselected;
            internal int CutIndex;
            internal bool Terminal;
        }

        private SimplificationCandidate FindSimplifiable(TreeNode node, NodeTopologySnapshot topology)
        {
            if (node is Always && GetNode(((Always)node).node) is Always innerAlways)
            {
                node = innerAlways;
            }

            if (node == null || (node is not Sequence && node is not Decision && node is not Condition
                && node is not Repeat && node is not Retry && node is not Always && node is not Inverter)
                || node is ServiceHostNode host && host.services != null && host.services.Count != 0)
            {
                return null;
            }

            IReadOnlyList<NodeReferenceOccurrence> nodeIncoming = topology.GetIncoming(node);
            if (nodeIncoming.Count > 1 || nodeIncoming.Any(item => item.Kind != NodeOwnershipKind.Structural)) return null;

            if (node is Sequence sequenceToFlatten)
            {
                Sequence inner = sequenceToFlatten.events?.Select(reference => GetNode(reference)).OfType<Sequence>()
                    .FirstOrDefault(sequence => IsFlattenable(sequence, topology));
                if (inner != null)
                {
                    return new SimplificationCandidate { Kind = SimplificationKind.Flatten, Wrapper = node, Flattened = inner };
                }
            }
            if (node is Decision decisionToFlatten)
            {
                Decision inner = decisionToFlatten.events?.Select(reference => GetNode(reference)).OfType<Decision>()
                    .FirstOrDefault(decision => IsFlattenable(decision, topology));
                if (inner != null)
                {
                    return new SimplificationCandidate { Kind = SimplificationKind.Flatten, Wrapper = node, Flattened = inner };
                }
            }

            if (node is Sequence sequenceToPrune && TryFindSequencePrune(sequenceToPrune, topology, out int sequenceCutIndex))
            {
                return new SimplificationCandidate
                {
                    Kind = SimplificationKind.PruneSequence,
                    Wrapper = node,
                    CutIndex = sequenceCutIndex,
                    Terminal = GetNode(sequenceToPrune.events[sequenceCutIndex]) is Constant sequenceTerminal && !sequenceTerminal.returnValue,
                };
            }
            if (node is Decision decisionToPrune && TryFindDecisionPrune(decisionToPrune, topology, out int decisionCutIndex))
            {
                return new SimplificationCandidate
                {
                    Kind = SimplificationKind.PruneDecision,
                    Wrapper = node,
                    CutIndex = decisionCutIndex,
                    Terminal = GetNode(decisionToPrune.events[decisionCutIndex]) is Constant decisionTerminal && decisionTerminal.returnValue,
                };
            }

            if (node is Condition condition)
            {
                TreeNode predicate = GetNode(condition.condition);
                TreeNode trueBranch = GetNode(condition.trueNode);
                TreeNode falseBranch = GetNode(condition.falseNode);
                if (predicate is Inverter inverter && GetNode(inverter.node) is TreeNode invertedPredicate
                    && CanDeleteNode(inverter, topology) && CanPromoteNode(invertedPredicate, topology))
                {
                    return new SimplificationCandidate
                    {
                        Kind = SimplificationKind.InvertConditionPredicate,
                        Wrapper = condition,
                        Child = invertedPredicate,
                        Predicate = inverter,
                        Incoming = nodeIncoming.Count == 1 ? nodeIncoming[0] : default,
                    };
                }
                if (predicate is Constant constant && CanDeleteNode(constant, topology))
                {
                    TreeNode selected = constant.returnValue ? trueBranch : falseBranch;
                    TreeNode unselected = constant.returnValue ? falseBranch : trueBranch;
                    if (selected == null || CanPromoteNode(selected, topology))
                    {
                        return new SimplificationCandidate
                        {
                            Kind = SimplificationKind.ConstantCondition,
                            Wrapper = condition,
                            Child = selected,
                            Predicate = constant,
                            Selected = selected,
                            Unselected = unselected,
                            Incoming = nodeIncoming.Count == 1 ? nodeIncoming[0] : default,
                        };
                    }
                }
                if (trueBranch is Sequence trueSequence && falseBranch == null
                    && IsServiceFree(trueSequence) && CanPromoteNode(trueSequence, topology)
                    && CanPromoteNode(predicate, topology))
                {
                    return new SimplificationCandidate
                    {
                        Kind = SimplificationKind.ConditionTrueSequence,
                        Wrapper = condition,
                        Child = trueSequence,
                        Predicate = predicate,
                        Incoming = nodeIncoming.Count == 1 ? nodeIncoming[0] : default,
                    };
                }
                if (falseBranch is Decision falseDecision && trueBranch == null
                    && IsServiceFree(falseDecision) && CanPromoteNode(falseDecision, topology)
                    && CanPromoteNode(predicate, topology))
                {
                    return new SimplificationCandidate
                    {
                        Kind = SimplificationKind.ConditionFalseDecision,
                        Wrapper = condition,
                        Child = falseDecision,
                        Predicate = predicate,
                        Incoming = nodeIncoming.Count == 1 ? nodeIncoming[0] : default,
                    };
                }
            }

            TreeNode child = node switch
            {
                Sequence sequence when sequence.events?.Length == 1 => GetNode(sequence.events[0]),
                Decision decision when decision.events?.Length == 1 => GetNode(decision.events[0]),
                Condition emptyCondition when emptyCondition.trueNode?.UUID == UUID.Empty && emptyCondition.falseNode?.UUID == UUID.Empty
                    => GetNode(emptyCondition.condition),
                Repeat repeat when repeat.repeatCount != null && repeat.repeatCount.IsConstant && repeat.repeatCount.Constant == 1
                    => GetNode(repeat.node),
                Retry retry when retry.maxAttempts != null && retry.maxAttempts.IsConstant && retry.maxAttempts.Constant == 1
                    => GetNode(retry.node),
                Always always when GetNode(always.node) is Always inner => GetNode(inner.node),
                Inverter inverter when GetNode(inverter.node) is Inverter => GetNode(inverter.node),
                _ => null,
            };
            if (child == null || child is Service || topology.GetRawIncomingCount(child) != 0
                || topology.GetRawIncomingCount(node) != 0)
            {
                return null;
            }

            IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
            if (incoming.Count > 1 || incoming.Any(item => item.Kind != NodeOwnershipKind.Structural))
            {
                return null;
            }

            if (node is Always && child == null)
            {
                return null;
            }

            return new SimplificationCandidate
            {
                Wrapper = node,
                Child = child,
                Incoming = incoming.Count == 1 ? incoming[0] : default,
                Kind = node is Inverter ? SimplificationKind.DoubleInverter : SimplificationKind.ReplaceWithChild,
            };
        }

        private bool ApplySimplification(SimplificationCandidate candidate)
        {
            if (candidate.Kind == SimplificationKind.PruneSequence || candidate.Kind == SimplificationKind.PruneDecision)
            {
                if (candidate.Wrapper is Sequence sequence)
                {
                    if (candidate.Terminal)
                    {
                        DetachReferences(sequence.events, candidate.CutIndex);
                        sequence.events = sequence.events.Take(candidate.CutIndex).ToArray();
                    }
                    else
                    {
                        GetNode(sequence.events[candidate.CutIndex]).parent = NodeReference.Empty;
                        sequence.events = sequence.events.Where((_, index) => index != candidate.CutIndex).ToArray();
                    }
                }
                else if (candidate.Wrapper is Decision decision)
                {
                    if (candidate.Terminal)
                    {
                        DetachReferences(decision.events, candidate.CutIndex + 1);
                        decision.events = decision.events.Take(candidate.CutIndex + 1).ToArray();
                    }
                    else
                    {
                        GetNode(decision.events[candidate.CutIndex]).parent = NodeReference.Empty;
                        decision.events = decision.events.Where((_, index) => index != candidate.CutIndex).ToArray();
                    }
                }
                RegenerateTable();
                return true;
            }

            if (candidate.Kind == SimplificationKind.InvertConditionPredicate)
            {
                Condition condition = (Condition)candidate.Wrapper;
                NodeReference trueBranch = condition.trueNode;
                condition.trueNode = condition.falseNode;
                condition.falseNode = trueBranch;
                condition.condition = new NodeReference(candidate.Child.uuid);
                candidate.Child.parent = new NodeReference(condition.uuid);
                candidate.Predicate.parent = NodeReference.Empty;
                nodes.Remove(candidate.Predicate);
                graphLayout?.RemoveNode(candidate.Predicate.uuid);
                RegenerateTable();
                return true;
            }

            if (candidate.Kind == SimplificationKind.ConditionTrueSequence || candidate.Kind == SimplificationKind.ConditionFalseDecision)
            {
                if (!ReplaceIncoming(candidate.Wrapper, candidate.Child, candidate.Incoming)) return false;
                if (candidate.Child is Sequence trueSequence)
                {
                    trueSequence.events = new[] { new NodeReference(candidate.Predicate.uuid) }
                        .Concat(trueSequence.events ?? Array.Empty<NodeReference>()).ToArray();
                }
                else if (candidate.Child is Decision falseDecision)
                {
                    falseDecision.events = new[] { new NodeReference(candidate.Predicate.uuid) }
                        .Concat(falseDecision.events ?? Array.Empty<NodeReference>()).ToArray();
                }
                candidate.Predicate.parent = new NodeReference(candidate.Child.uuid);
                RemoveNode(candidate.Wrapper);
                return true;
            }

            if (candidate.Kind == SimplificationKind.ConstantCondition)
            {
                if (candidate.Selected == null)
                {
                    if (!ReplaceIncoming(candidate.Wrapper, candidate.Predicate, candidate.Incoming)) return false;
                }
                else if (!ReplaceIncoming(candidate.Wrapper, candidate.Selected, candidate.Incoming))
                {
                    return false;
                }
                if (candidate.Unselected != null) candidate.Unselected.parent = NodeReference.Empty;
                RemoveNode(candidate.Wrapper);
                if (candidate.Selected != null) RemoveNode(candidate.Predicate);
                return true;
            }

            if (candidate.Kind == SimplificationKind.DoubleInverter)
            {
                Inverter outer = (Inverter)candidate.Wrapper;
                Inverter inner = (Inverter)candidate.Child;
                TreeNode grandChild = GetNode(inner.node);
                if (grandChild == null || !ReplaceIncoming(outer, grandChild, candidate.Incoming)) return false;
                RemoveNode(outer);
                RemoveNode(inner);
                return true;
            }

            if (candidate.Flattened != null)
            {
                if (candidate.Wrapper is Sequence outerSequence && candidate.Flattened is Sequence innerSequence)
                {
                    outerSequence.events = outerSequence.events.SelectMany(reference => reference.UUID == innerSequence.uuid
                        ? innerSequence.events : new[] { reference }).ToArray();
                }
                else if (candidate.Wrapper is Decision outerDecision && candidate.Flattened is Decision innerDecision)
                {
                    outerDecision.events = outerDecision.events.SelectMany(reference => reference.UUID == innerDecision.uuid
                        ? innerDecision.events : new[] { reference }).ToArray();
                }
                innerSequenceOrDecisionParentless(candidate.Flattened);
                nodes.Remove(candidate.Flattened);
                graphLayout?.RemoveNode(candidate.Flattened.uuid);
                RegenerateTable();
                return true;
            }

            if (candidate.Incoming.Owner == null)
            {
                if (headNodeUUID != candidate.Wrapper.uuid) return false;
                headNodeUUID = candidate.Child.uuid;
                candidate.Child.parent = NodeReference.Empty;
            }
            else
            {
                SetReference(candidate.Incoming.Owner, candidate.Incoming.Address.FieldName,
                    candidate.Incoming.Address.Index, candidate.Child);
                candidate.Child.parent = new NodeReference(candidate.Incoming.Owner.uuid);
            }

            candidate.Wrapper.parent = NodeReference.Empty;
            nodes.Remove(candidate.Wrapper);
            graphLayout?.RemoveNode(candidate.Wrapper.uuid);
            RegenerateTable();
            return true;
        }

        private bool ReplaceIncoming(TreeNode wrapper, TreeNode child, NodeReferenceOccurrence incoming)
        {
            if (incoming.Owner == null)
            {
                if (headNodeUUID != wrapper.uuid) return false;
                headNodeUUID = child.uuid;
                child.parent = NodeReference.Empty;
            }
            else
            {
                SetReference(incoming.Owner, incoming.Address.FieldName, incoming.Address.Index, child);
                child.parent = new NodeReference(incoming.Owner.uuid);
            }
            return true;
        }

        private void RemoveNode(TreeNode node)
        {
            if (node == null) return;
            node.parent = NodeReference.Empty;
            nodes.Remove(node);
            graphLayout?.RemoveNode(node.uuid);
            RegenerateTable();
        }

        private bool CanDeleteNode(TreeNode node, NodeTopologySnapshot topology)
        {
            return node != null && topology.GetRawIncomingCount(node) == 0
                && topology.GetIncoming(node).Count <= 1;
        }

        private bool CanPromoteNode(TreeNode node, NodeTopologySnapshot topology)
        {
            return node != null && node is not Service && topology.GetRawIncomingCount(node) == 0;
        }

        private static bool IsServiceFree(TreeNode node)
        {
            return node is not ServiceHostNode host || host.services == null || host.services.Count == 0;
        }

        private void DetachReferences(NodeReference[] references, int startIndex)
        {
            if (references == null) return;
            for (int index = startIndex; index < references.Length; index++)
            {
                TreeNode detached = GetNode(references[index]);
                if (detached != null) detached.parent = NodeReference.Empty;
            }
        }

        private bool TryFindSequencePrune(Sequence sequence, NodeTopologySnapshot topology, out int cutIndex)
        {
            cutIndex = -1;
            if (sequence.events == null) return false;
            for (int index = 0; index < sequence.events.Length; index++)
            {
                TreeNode node = GetNode(sequence.events[index]);
                if (node == null || topology.GetRawIncomingCount(node) != 0) return false;
                if (node is Constant constant && constant.returnValue)
                {
                    cutIndex = index;
                    continue;
                }
                if (node is Constant constantFailure && !constantFailure.returnValue)
                {
                    cutIndex = index;
                    return index > 0 || sequence.events.Length > 1;
                }
            }
            return cutIndex >= 0;
        }

        private bool TryFindDecisionPrune(Decision decision, NodeTopologySnapshot topology, out int cutIndex)
        {
            cutIndex = -1;
            if (decision.events == null) return false;
            for (int index = 0; index < decision.events.Length; index++)
            {
                TreeNode node = GetNode(decision.events[index]);
                if (node == null || topology.GetRawIncomingCount(node) != 0) return false;
                if (node is Constant constant && !constant.returnValue)
                {
                    cutIndex = index;
                    continue;
                }
                if (node is Constant constantSuccess && constantSuccess.returnValue)
                {
                    cutIndex = index;
                    return index > 0 || decision.events.Length > 1;
                }
            }
            return cutIndex >= 0;
        }

        private bool IsFlattenable(TreeNode node, NodeTopologySnapshot topology)
        {
            return node is ServiceHostNode host && (host.services == null || host.services.Count == 0)
                && topology.GetRawIncomingCount(node) == 0
                && topology.GetIncoming(node).Count == 1;
        }

        private static void innerSequenceOrDecisionParentless(TreeNode node)
        {
            node.parent = NodeReference.Empty;
        }
    }
}
#endif
