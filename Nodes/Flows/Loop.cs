using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
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
        public bool isExecutingCondition;
        public TreeNode current;
        public int currentCount;

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
                else
                {
                    //if there are something to start
                    if (events.Count > 0)
                    {
                        current = events[0];
                        SetNextExecute(current);
                    }
                    //if not, check condition again or return false
                    else
                    {
                        behaviourTree.WaitForNextFrame();
                    }
                }
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
                bool condition = currentCount++ < loopCount;
                //Debug.Log("Current Count " + currentCount);
                ReceiveReturnFromChild(condition);
                //{
                //    if (events.Count > 0)
                //    {
                //        current = events[0];
                //        SetNextExecute(current);
                //    }
                //    else behaviourTree.WaitForNextFrame();
                //}
                //else
                //{
                //    Return(currentCount != 1);
                //}
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


        public override void Initialize()
        {
            isExecutingCondition = false;
            current = null;
            currentCount = 0;
            condition = behaviourTree.References[condition.uuid];
            for (int i = 0; i < events.Count; i++)
            {
                NodeReference item = events[i];
                events[i] = behaviourTree.References[item];
            }
        }
    }
}