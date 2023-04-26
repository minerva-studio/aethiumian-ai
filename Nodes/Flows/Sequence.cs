using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections.Generic;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// node that will execute all its child
    /// </summary>
    [Serializable]
    [NodeTip("A sequence, always execute a list of nodes in order")]
    public sealed class Sequence : Flow
    {
        [ReadOnly] public List<NodeReference> events;
        [ReadOnly] TreeNode current;
        [ReadOnly] public bool hasTrue;

        public Sequence()
        {
            events = new();
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (events.IndexOf(current) == events.Count - 1)
            {
                return StateOf(hasTrue);
            }
            else
            {
                hasTrue |= @return;
                current = events[events.IndexOf(current) + 1];
                return SetNextExecute(current);
            }
        }

        public sealed override State Execute()
        {
            hasTrue = false;
            if (events.Count == 0)
            {
                return State.Failed;
            }
            else
            {
                current = events[0];
                return SetNextExecute(current);
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

        /**
         * - Sequence
         *   - store enemyCount from GetEnemyCount(); [Node]
         *   - condition
         *     - if enemyCount > 3
         *     - true: ()
         */
    }
}