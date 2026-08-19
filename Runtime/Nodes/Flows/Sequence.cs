using Aethiumian.AI.Attributes;
using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// Executes children in order until one child fails.
    /// </summary>
    [Serializable]
    [NodeTip("Executes children in order until one child fails. Returns success when every child succeeds.")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Sequence : Flow
    {
        [ReadOnly] public NodeReference[] events;
        [ReadOnly] NodeReference current;
        [ReadOnly] int index;

        public Sequence()
        {
            events = new NodeReference[0];
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (!@return)
            {
                return State.Failed;
            }

            if (index == events.Length - 1)
            {
                return State.Success;
            }

            index++;
            current = events[index];
            return SetNextExecute(current);
        }

        public sealed override State Execute()
        {
            if (events.Length == 0)
            {
                return State.Success;
            }
            current = events[0];
            index = 0;
            return SetNextExecute(current);
        }

        public override void Initialize()
        {
            index = -1;
            current = null;
        }
    }
}
