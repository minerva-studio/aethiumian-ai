using System;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// execute two node by given condition
    /// <br></br>
    /// return true when the true/false branch node return true, false otherwise
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public class Condition : Flow
    {
        public NodeReference condition;
        public NodeReference trueNode;
        public NodeReference falseNode;
        [Header("info")]
        bool checkCondition = false;

        public override void ReceiveReturnFromChild(bool @return)
        {
            if (checkCondition)
            {
                End(@return);
                return;
            }

            if (@return)
            {
                if (trueNode.HasReference)
                {
                    SetNextExecute(trueNode);
                    checkCondition = true;
                    return;
                }
            }
            else if (falseNode.HasReference)
            {
                SetNextExecute(falseNode);
                checkCondition = true;
                return;
            }
            End(@return);
        }

        public override void Execute()
        {
            //AddSelfToProgress();
            checkCondition = false;
            SetNextExecute(condition);
        }

        public override void Initialize()
        {
            checkCondition = false;
            //Debug.Log(conditionUUID);
            //Debug.Log(trueNodeUUID);
            //Debug.Log(falseNodeUUID);
            condition = behaviourTree.References[condition.uuid];
            trueNode = behaviourTree.References[trueNode.uuid];
            falseNode = behaviourTree.References[falseNode.uuid];
        }
    }
}