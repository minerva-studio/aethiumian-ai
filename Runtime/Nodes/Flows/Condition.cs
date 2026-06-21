using Aethiumian.AI.References;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// execute two node by given condition
    /// <br/>
    /// return true when the true/false branch node return true, false otherwise
    /// </summary>
    [Serializable]
    [NodeTip("An if-else structure")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
                if (behaviourTree.GetNode(trueNode) != null)
                {
                    checkCondition = true;
                    return SetNextExecute(trueNode);
                }
                else return State.Success;
            }
            else
            {
                if (behaviourTree.GetNode(falseNode) != null)
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
        }
    }
}
