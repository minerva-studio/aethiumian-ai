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
    public sealed class Probability : Flow, IListFlow
    {
        public List<EventWeight> events = new List<EventWeight>();

        [Serializable]
        public class EventWeight : ICloneable, INodeConnection, INodeReference, IWeightable<NodeReference>
        {
            public int weight;
            public NodeReference reference;

            public EventWeight()
            {
                reference = NodeReference.Empty;
            }

            public int Weight => weight;
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


        int IListFlow.Count => events.Count;

        void IListFlow.Add(TreeNode treeNode)
        {
            events.Add(new EventWeight() { reference = treeNode, weight = 1 });
            treeNode.parent.UUID = uuid;
        }

        void IListFlow.Insert(int index, TreeNode treeNode)
        {
            int weight = 1;
            if (events.Count > index && index > 0) { weight = events[index].weight; }
            events.Insert(index, new EventWeight() { reference = treeNode, weight = weight });
            treeNode.parent.UUID = uuid;
        }

        int IListFlow.IndexOf(TreeNode treeNode)
        {
            return events.FindIndex(n => n.reference == treeNode);
        }
    }
}