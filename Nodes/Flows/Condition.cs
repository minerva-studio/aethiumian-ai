using Amlos.AI.References;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// execute two node by given condition
    /// <br/>
    /// return true when the true/false branch node return true, false otherwise
    /// </summary>
    [Serializable]
    [NodeTip("An if-else structure")]
    public sealed class Condition : Flow
    {
        public NodeReference condition;
        public NodeReference trueNode;
        public NodeReference falseNode;
        [Header("info")]
        bool checkCondition = false;

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (checkCondition)
            {
                return StateOf(@return);
            }

            return ExecuteBranch(@return);
        }

        private State ExecuteBranch(bool @return)
        {
            if (@return)
            {
                if (trueNode.HasReference)
                {
                    checkCondition = true;
                    return SetNextExecute(trueNode);
                }
                else return State.Success;
            }
            else
            {
                if (falseNode.HasReference)
                {
                    checkCondition = true;
                    return SetNextExecute(falseNode);
                }
                else return State.Failed;
            }
        }

        public override State Execute()
        {
            checkCondition = false;
            return SetNextExecute(condition);
        }

        public override void Initialize()
        {
            checkCondition = false;
            //Debug.Log(conditionUUID);
            //Debug.Log(trueNodeUUID);
            //Debug.Log(falseNodeUUID);
            behaviourTree.GetNode(ref condition);//.References[condition.UUID];
            behaviourTree.GetNode(ref trueNode);
            behaviourTree.GetNode(ref falseNode);
        }
    }
}