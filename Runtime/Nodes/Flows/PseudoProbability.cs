using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.WeightedRandom;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// node that random goto 1 next step
    /// </summary>
    [Serializable]
    [NodeTip("Execute one of child by chance once, with some variable conditions and fake randomness")]
    public sealed class PseudoProbability : Flow, IListFlow
    {
        public EventWeight[] events = new EventWeight[0];
        public VariableField<int> maxConsecutiveBranch = -1;

        EventWeight previous;
        int consecutiveCount;

        [Serializable]
        public class EventWeight : ICloneable, INodeConnection, INodeReference, IWeightable<NodeReference>
        {
            public VariableField<int> weight;
            public NodeReference reference;

            public EventWeight()
            {
                reference = NodeReference.Empty;
            }

            public int Weight => Mathf.Max(0, weight);
            public NodeReference Item => reference;
            object IWeightable.Item => reference;
            public UUID UUID { get => reference.UUID; set => reference.UUID = value; }
            public TreeNode Node { get => reference.Node; set => reference.Node = value; }
            public bool IsRawReference => reference.IsRawReference;
            public bool HasEditorReference => reference.HasEditorReference;
            public bool HasReference => reference.HasReference;




            public object Clone()
            {
                return new EventWeight() { weight = weight, reference = reference.Clone() };
            }

            public void SetWeight(int weight) { this.weight = weight; }
        }

        public sealed override State Execute()
        {
            // has execute too many times
            int max = this.maxConsecutiveBranch;
            EventWeight eventWeight;
            if (max > 0 && consecutiveCount >= max)
            {
                var biasedEvent = new List<EventWeight>(events);
                biasedEvent.Remove(this.previous);
                eventWeight = biasedEvent.WeightNode();
            }
            else eventWeight = events.WeightNode();
            if (eventWeight == null) return State.Failed;

            // recording 
            if (this.previous == eventWeight)
            {
                this.consecutiveCount++;
            }
            else
            {
                this.previous = eventWeight;
                this.consecutiveCount = 1;
            }
            TreeNode treeNode = eventWeight?.reference;
            return SetNextExecute(treeNode);
        }

        public override TreeNode Clone()
        {
            var node = base.Clone() as PseudoProbability;
            node.events = node.events.Select(e => (EventWeight)e.Clone()).ToArray();
            return node;
        }

        public override void Initialize()
        {
            previous = null;
            consecutiveCount = 0;

            // initialize events
            for (int i = 0; i < events.Length; i++)
            {
                behaviourTree.GetNode(ref events[i]);
                VariableField<int> weight = events[i].weight;
                weight.SetRuntimeReference(behaviourTree.Variables[weight.UUID]);
            }
        }


        int IListFlow.Count => events.Length;

        void IListFlow.Add(TreeNode treeNode)
        {
            ArrayUtility.Add(ref events, new EventWeight() { reference = treeNode, weight = 1 });
            treeNode.parent.UUID = uuid;
        }

        void IListFlow.Insert(int index, TreeNode treeNode)
        {
            int weight = 1;
            if (events.Length > index && index > 0) { weight = events[index].weight; }
            ArrayUtility.Insert(ref events, index, new EventWeight() { reference = treeNode, weight = weight });
            treeNode.parent.UUID = uuid;
        }

        int IListFlow.IndexOf(TreeNode treeNode)
        {
            return events.FindIndex(n => n.reference.IsPointTo(treeNode));
        }

        void IListFlow.Remove(Amlos.AI.Nodes.TreeNode treeNode)
        {
            var weight = events.FirstOrDefault(n => n.reference.IsPointTo(treeNode));
            ArrayUtility.Remove(ref events, weight);
        }
    }
}