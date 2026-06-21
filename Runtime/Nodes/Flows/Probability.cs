using Aethiumian.AI.References;
using Minerva.Module;
using Minerva.Module.WeightedRandom;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// node that random goto 1 next step
    /// </summary>
    [Serializable]
    [NodeTip("Execute one of child by chance once")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Probability : Flow
    {
        public EventWeight[] events = new EventWeight[0];

        [Serializable]
        public class EventWeight : ICloneable, INodeConnection, INodeReference, IWeightable<NodeReference>
        {
            public int weight;
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
                return Duplicate();
            }

            public object Duplicate()
            {
                return new EventWeight() { weight = weight, reference = Accessors.Duplicate.Value(reference) };
            }

            public void SetWeight(int weight) { this.weight = weight; }
        }

        public sealed override State Execute()
        {
            var reference = events.WeightNode()?.reference;
            return SetNextExecute(reference);
        }

        public override void Initialize()
        {
            for (int i = 0; i < events.Length; i++)
            {
                behaviourTree.GetNode(ref events[i]);
            }
            //events.ForEach((e) => e.@event.Initialize());
        }
    }
}
