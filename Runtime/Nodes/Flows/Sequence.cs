using Amlos.AI.References;
using Minerva.Module;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// node that will execute all its child
    /// </summary>
    [Serializable]
    [NodeTip("A sequence, always execute a list of nodes in order")]
    public sealed class Sequence : Flow, IListFlow
    {
        [ReadOnly] public NodeReference[] events;
        [ReadOnly] public bool hasTrue;
        [ReadOnly] TreeNode current;
        [ReadOnly] int index;

        public Sequence()
        {
            events = new NodeReference[0];
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (index == events.Length - 1)
            {
                return StateOf(hasTrue);
            }
            else
            {
                hasTrue |= @return;
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



        int IListFlow.Count => events.Length;

        void IListFlow.Add(TreeNode treeNode)
        {
            ArrayUtility.Add(ref events, treeNode);
            treeNode.parent.UUID = uuid;
        }

        void IListFlow.Insert(int index, TreeNode treeNode)
        {
            ArrayUtility.Insert(ref events, index, treeNode);
            treeNode.parent.UUID = uuid;
        }

        int IListFlow.IndexOf(TreeNode treeNode)
        {
            return Array.IndexOf(events, treeNode);
        }

        void IListFlow.Remove(Amlos.AI.Nodes.TreeNode treeNode)
        {
            ArrayUtility.Remove(ref events, treeNode);
        }
    }
}