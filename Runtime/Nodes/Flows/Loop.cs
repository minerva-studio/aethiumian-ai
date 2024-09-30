using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{

    /// <summary>
    /// node that loop its child until the given condition is false
    /// </summary>
    [Serializable]
    [NodeTip("A loop, can be either repeat by given number of times or matching certain condition")]
    public sealed class Loop : Flow, IListFlow
    {
        public enum LoopType
        {
            @while,
            @for,
            @doWhile
        }

        public LoopType loopType;
        public NodeReference condition;

        public NodeReference[] events;
        public VariableField<int> loopCount;

        [Header("Info")]
        private bool isExecutingCondition;
        private int index;
        private TreeNode current;
        private int currentCount;
        private int startFrame;

        public Loop()
        {
            loopCount = new VariableField<int>();
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            return MoveToNextAction(@return);
        }

        private State MoveToNextAction(bool @return)
        {
            if (isExecutingCondition)
            {
                isExecutingCondition = false;
                //Loop condition failed, return
                if (!@return)
                {
                    //Debug.LogError("Loop condition failed");
                    return State.Success;
                }
                //start loop content 
                else
                {
                    // nothing in the loop, continue checking condition
                    if (events.Length == 0 && startFrame == Time.frameCount)
                    {
                        return StartCheckCondition();
                    }

                    //Debug.LogError("Loop condition success"); 
                    current = null;
                    return MoveNext();
                }
            }
            //in loop already
            else
            {
                if (HasNext())
                {
                    return MoveNext();
                }
                //check condition
                else
                {
                    return StartCheckCondition();
                }
            }
        }

        private bool HasNext()
        {
            return index < events.Length - 1;
        }

        private State MoveNext()
        {
            if (current == null)
            {
                index = 0;
                current = events[0];
            }
            else
            {
                index++;
                current = events[index];
            }

            return SetNextExecute(current);
        }

        private State StartCheckCondition()
        {
            isExecutingCondition = true;
            if (loopType == LoopType.@for)
            {
                bool condition = currentCount++ < loopCount;
                //Debug.Log("Current Count " + currentCount);
                return MoveToNextAction(condition);
            }
            else
            {
                current = condition;
                return SetNextExecute(current);
            }
        }

        public override sealed State Execute()
        {
            index = 0;
            currentCount = 0;
            current = null;
            isExecutingCondition = false;
            startFrame = Time.frameCount;
            //AddSelfToProgress();
            switch (loopType)
            {
                case LoopType.@while:
                case LoopType.@for:
                    return StartCheckCondition();
                case LoopType.doWhile:
                    return MoveNext();
                default:
                    return State.Failed;
            }
        }

        //public override void End(bool @return)
        //{
        //    returnValue = @return;
        //    behaviourTree.FixedUpdateCall += NextUpdateEnd;
        //}

        //public void NextUpdateEnd()
        //{
        //    behaviourTree.FixedUpdateCall -= NextUpdateEnd;
        //    base.End(returnValue);
        //}


        public override void Initialize()
        {
            isExecutingCondition = false;
            index = -1;
            current = null;
            currentCount = 0;
            behaviourTree.GetNode(ref condition);
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