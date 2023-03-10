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
                    Debug.LogError("Loop condition failed");
                    return State.Success;
                }
                //start loop content 
                else
                {
                    Debug.LogError("Loop condition success");
                    current = events[0];
                    return SetNextExecute(current);
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
            return events.IndexOf(current) != events.Count - 1;
        }

        private State MoveNext()
        {
            if (current == null) current = events[0];
            else current = events[events.IndexOf(current) + 1];
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
            currentCount = 0;
            current = null;
            isExecutingCondition = false;
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