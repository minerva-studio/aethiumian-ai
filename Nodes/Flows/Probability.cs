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
    public sealed class Probability : Flow
    {
        public List<EventWeight> events = new List<EventWeight>();

        [Serializable]
        public class EventWeight : IWeightable<NodeReference>, ICloneable
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

        public override void Execute()
        {
            //AddSelfToProgress();
            TreeNode treeNode = events.WeightNode()?.reference;

            SetNextExecute(treeNode);
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