using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module.WeightedRandom;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// node that random goto 1 next step
    /// </summary>
    [Serializable]
    [NodeTip("Execute one of child by chance once, with some variable conditions and fake randomness")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class PseudoProbability : Flow
    {
        public EventWeight[] events = new EventWeight[0];
        public VariableField<int> maxConsecutiveBranch = -1;

        EventWeight previous;
        int consecutiveCount;

        [Serializable]
        public class EventWeight : ICloneable, INodeConnection, INodeReference, IVariableField, IWeightable<NodeReference>
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

            private VariableField<int> WeightField => weight ??= new VariableField<int>();

            VariableType IVariableField.Type => WeightField.Type;

            UUID IVariableField.UUID => WeightField.UUID;

            bool IVariableField.IsConstant => WeightField.IsConstant;

            Variable IVariableField.Variable => WeightField.Variable;

            object IVariableField.Value => WeightField.Value;

            void IVariableField.SetReference(VariableData variable)
            {
                WeightField.SetReference(variable);
            }

            void IVariableField.SetRuntimeReference(Variable variable)
            {
                WeightField.SetRuntimeReference(variable);
            }




            public object Clone()
            {
                return Duplicate();
            }

            public object Duplicate()
            {
                return new EventWeight() { weight = global::Aethiumian.AI.Accessors.Duplicate.Value(weight), reference = global::Aethiumian.AI.Accessors.Duplicate.Value(reference) };
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
            var reference = eventWeight?.reference;
            return SetNextExecute(reference);
        }

        public override void Initialize()
        {
            previous = null;
            consecutiveCount = 0;
        }
    }
}
