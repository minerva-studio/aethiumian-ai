using Minerva.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI
{
    /// <summary>
    /// node that will execute all its child
    /// </summary>
    [Serializable]
    public sealed class Sequence : Flow
    {
        [ReadOnly] public List<NodeReference> events;
        [ReadOnly] TreeNode current;
        [ReadOnly] public bool hasTrue;

        public Sequence()
        {
            events = new();
        }

        public override void ReceiveReturnFromChild(bool @return)
        {
            if (events.IndexOf(current) == events.Count - 1)
            {
                End(hasTrue);
            }
            else
            {
                hasTrue |= @return;
                current = events[events.IndexOf(current) + 1];
                SetNextExecute(current);
            }
        }

        public override void Execute()
        {
            //AddSelfToProgress();
            hasTrue = false;
            if (events.Count == 0)
            {
                End(false);
            }
            else
            {
                current = events[0];
                SetNextExecute(current);
            }
        }

        public override void Initialize()
        {
            current = null;
            hasTrue = false;
            for (int i = 0; i < events.Count; i++)
            {
                NodeReference item = events[i];
                events[i] = behaviourTree.References[item];
            }
        }
    }
}