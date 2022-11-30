using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// execute children in order until one node return true (if elseif else)
    /// <br></br>
    /// return true if any execution result true, false if all nodes execution result false
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public sealed class Decision : Flow
    {
        public List<NodeReference> events;
        [Header("info")]
        TreeNode current;

        public override void ReceiveReturnFromChild(bool @return)
        {
            if (@return)
            {
                End(true);
            }
            else if (events.IndexOf(current) == events.Count - 1)
            {
                End(false);
            }
            else
            {
                current = events[events.IndexOf(current) + 1];
                SetNextExecute(current);
            }
        }

        public override void Execute()
        {
            //AddSelfToProgress();
            current = events[0];
            SetNextExecute(current);
        }

        public override void Initialize()
        {
            current = null;
            events = new List<NodeReference>();
            for (int i = 0; i < events.Count; i++)
            {
                NodeReference item = events[i];
                events[i] = behaviourTree.References[item];
            }
        }
    }
}