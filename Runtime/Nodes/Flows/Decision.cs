﻿using Amlos.AI.References;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// execute children in order until one node return true (if elseif else)
    /// <br/>
    /// return true if any execution result true, false if all nodes execution result false
    /// </summary>
    [Serializable]
    [NodeTip("Create a decision making process, execute a list of nodes in order until one child node return true")]
    public sealed class Decision : Flow, IListFlow
    {
        public NodeReference[] events;
        [Header("info")]
        [ReadOnly] TreeNode current;
        [ReadOnly] int index;

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (@return)
            {
                return State.Success;
            }
            else if (index == events.Length - 1)
            {
                return State.Failed;
            }
            else
            {
                index++;
                current = events[index];
                return SetNextExecute(current);
            }
        }

        public override State Execute()
        {
            if (events.Length == 0)
            {
                return State.Failed;
            }
            index = 0;
            current = events[0];
            return SetNextExecute(current);
        }

        public override void Initialize()
        {
            index = -1;
            current = null;
            for (int i = 0; i < events.Length; i++)
            {
                behaviourTree.GetNode(ref events[i]);
            }
        }




        int IListFlow.Count => events.Length;

        void IListFlow.Add(TreeNode treeNode)
        {
            ArrayUtility.Add(ref events, treeNode);
        }

        void IListFlow.Insert(int index, TreeNode treeNode)
        {
            ArrayUtility.Insert(ref events, index, treeNode);
            treeNode.parent.UUID = uuid;
        }

        void IListFlow.Remove(TreeNode treeNode)
        {
            ArrayUtility.Remove(ref events, treeNode);
        }

        int IListFlow.IndexOf(TreeNode treeNode) => Array.IndexOf(events, treeNode);
    }
}