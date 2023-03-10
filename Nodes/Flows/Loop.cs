using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{

    /// <summary>
    /// node that loop its child until the given condition is false
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public sealed class Loop : Flow
    {
        public enum LoopType
        {
            @while,
            @for,
            @doWhile
        }

        public LoopType loopType;
        public NodeReference condition;

        public List<NodeReference> events;
        public VariableField<int> loopCount;

        [Header("Info")]
        private bool isExecutingCondition;
        private TreeNode current;
        private int currentCount;
        private bool returnValue;

        public Loop()
        {
            loopCount = new VariableField<int>();
        }

        public override void ReceiveReturnFromChild(bool @return)
        {
            if (!isExecutingCondition)
            {
                if (HasNext())
                {
                    MoveNext();
                }
                //check condition
                else
                {
                    StartCheckCondition();
                }
            }
            //in loop already
            else
            {
                isExecutingCondition = false;
                //Loop condition failed, return
                if (!@return)
                {
                    End(true);
                }
                //start loop content 
                current = events[0];
                SetNextExecute(current);
            }
        }

        private bool HasNext()
        {
            return events.IndexOf(current) != events.Count - 1;
        }

        private void MoveNext()
        {
            if (current == null) current = events[0];
            else current = events[events.IndexOf(current) + 1];
            SetNextExecute(current);
        }

        private void StartCheckCondition()
        {
            isExecutingCondition = true;
            if (loopType == LoopType.@for)
            {
                bool condition = ++currentCount < loopCount;
                //Debug.Log("Current Count " + currentCount);
                ReceiveReturnFromChild(condition);
            }
            else
            {
                current = condition;
                SetNextExecute(current);
            }
        }

        public override void Execute()
        {
            currentCount = 0;
            current = null;
            isExecutingCondition = false;
            //AddSelfToProgress();
            switch (loopType)
            {
                case LoopType.@while:
                case LoopType.@for:
                    StartCheckCondition();
                    break;
                case LoopType.doWhile:
                    MoveNext();
                    break;
                default:
                    break;
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
            current = null;
            currentCount = 0;
            condition = behaviourTree.References[condition.UUID];
            for (int i = 0; i < events.Count; i++)
            {
                NodeReference item = events[i];
                events[i] = behaviourTree.References[item];
            }
        }
    }
}