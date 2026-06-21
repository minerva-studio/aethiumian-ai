using Aethiumian.AI.References;
using Minerva.Module;
using System;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// node that will execute all its child
    /// </summary>
    [Serializable]
    [NodeTip("A sequence, always execute a list of nodes in order")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Sequence : Flow
    {
        [ReadOnly] public NodeReference[] events;
        [ReadOnly] public bool hasTrue;
        [ReadOnly] NodeReference current;
        [ReadOnly] int index;

        public Sequence()
        {
            events = new NodeReference[0];
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            hasTrue |= @return;
            if (index == events.Length - 1)
            {
                return StateOf(hasTrue);
            }
            else
            {
                index++;
                current = events[index];
                return SetNextExecute(current);
            }
        }

        public sealed override State Execute()
        {
            hasTrue = false;
            if (events.Length == 0)
            {
                return State.Failed;
            }
            current = events[0];
            index = 0;
            return SetNextExecute(current);
        }

        public override void Initialize()
        {
            index = -1;
            current = null;
            hasTrue = false;
            for (int i = 0; i < events.Length; i++)
            {
                behaviourTree.GetNode(ref events[i]);
            }
        }
    }
}
