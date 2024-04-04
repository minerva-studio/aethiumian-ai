using Amlos.AI.References;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// execute children in order until one node return true (if elseif else)
    /// <br/>
    /// return true if any execution result true, false if all nodes execution result false
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    [NodeTip("Create a decision making process, execute a list of nodes in order until one child node return true")]
    public sealed class Decision : Flow, IListFlow
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
            if (events.Count == 0)
            {
                return State.Failed;
            }
            current = events[0];
            return SetNextExecute(current);
        }

        public override void Initialize()
        {
            current = null;
            for (int i = 0; i < events.Count; i++)
            {
                NodeReference item = events[i];
                events[i] = behaviourTree.References[item];
            }
        }




        int IListFlow.Count => events.Count;

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Add reference to given tree node
        /// </summary>
        /// <param name="treeNode"></param>
        void IListFlow.Add(TreeNode treeNode)
        {
            events.Add(treeNode);
            treeNode.parent.UUID = uuid;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Insert reference to given tree node at position
        /// </summary>
        /// <param name="treeNode"></param>
        void IListFlow.Insert(int index, TreeNode treeNode)
        {
            events.Insert(index, treeNode);
            treeNode.parent.UUID = uuid;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get the index of the given node
        /// </summary>
        /// <param name="treeNode"></param>
        int IListFlow.IndexOf(TreeNode treeNode) => events.IndexOf(treeNode);
    }
}