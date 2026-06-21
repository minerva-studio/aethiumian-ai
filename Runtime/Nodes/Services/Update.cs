using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [Serializable]
    [NodeTip("Repeat executing a subtree")]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Update : RepeatService
    {
        [Tooltip("Whether routine should start even if old one is not done yet")]
        public VariableField<bool> forceStopped;
        public NodeReference subtreeHead;
        private bool isRunning;


        public override bool IsReady => forceStopped ? base.IsReady : (base.IsReady && !isRunning);

        public override State Execute()
        {
            ResetTimer();
            isRunning = true;
            if (behaviourTree.GetNode(subtreeHead) != null) return SetNextExecute(subtreeHead);
            else return State.Failed;
        }

        public override void ReceiveReturn(bool @return)
        {
            isRunning = false;
        }

        protected override void OnStop()
        {
            isRunning = false;
        }

        public override void OnRegistered()
        {
            isRunning = false;
        }

        public override void OnUnregistered()
        {
            isRunning = false;
        }

        public override void Initialize()
        {
        }
    }
}
