using Amlos.AI.References;
using Minerva.Module;
using Minerva.Module.WeightedRandom;
using System;
using System.Collections.Generic;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// node that random goto 1 next step
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    [NodeTip("Execute one of child by chance once")]
    public sealed class Probability : Flow
    {
        public List<EventWeight> events = new List<EventWeight>();

        [Serializable]
        public class EventWeight : ICloneable, INodeConnection, IWeightable<NodeReference>
        {
            public int weight;
            public NodeReference reference;

            public int Weight => weight;
            public NodeReference Item => reference;
            object IWeightable.Item => reference;

            public object Clone()
            {
                return new EventWeight() { weight = weight, reference = reference.Clone() };
            }

            public void SetWeight(int weight) { this.weight = weight; }
        }


        public void AddEvent(TreeNode data, int weight)
        {
            events.Add(new EventWeight() { reference = data, weight = weight });
        }

        public sealed override State Execute()
        {
            TreeNode treeNode = events.WeightNode()?.reference;
            return SetNextExecute(treeNode);
        }

        public override TreeNode Clone()
        {
            var node = base.Clone() as Probability;
            node.events = node.events.DeepCloneToList();
            return node;
        }

        public override void Initialize()
        {
            events.ForEach((e) => e.reference = behaviourTree.References[e.reference]);
            //events.ForEach((e) => e.@event.Initialize());
        }
    }
}