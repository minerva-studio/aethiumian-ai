using Amlos.AI.References;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// execute children in order until one node return true (if elseif else)
    /// <br></br>
    /// return true if any execution result true, false if all nodes execution result false
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    [NodeTip("Create a decision making process, execute a list of nodes in order until one child node return true")]
    public sealed class Decision : Flow
    {
        public List<NodeReference> events;
        [Header("info")]
        TreeNode current;

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (@return)
            {
                return State.Success;
            }
            else if (events.IndexOf(current) == events.Count - 1)
            {
                return State.Failed;
            }
            else
            {
                current = events[events.IndexOf(current) + 1];
                return SetNextExecute(current);
            }
        }

        public override State Execute()
        {
            current = events[0];
            return SetNextExecute(current);
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