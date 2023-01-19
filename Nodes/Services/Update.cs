using System;

namespace Amlos.AI
{
    [Serializable]
    [NodeTip("Repeat executing a subtree")]
    public sealed class Update : RepeatService
    {
        public VariableField<bool> forceStopped;
        public NodeReference subtreeHead;
        private bool isRunning;


        public override bool IsReady => forceStopped ? base.IsReady : !isRunning;

        public override void Execute()
        {
            isRunning = true;
            if (subtreeHead.HasReference) SetNextExecute(subtreeHead);
            else End(false);
        }

        public override void ReceiveReturnFromChild(bool @return)
        {
            isRunning = false;
            base.ReceiveReturnFromChild(@return);
        }

        public override void Stop()
        {
            isRunning = false;
            base.Stop();
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
            subtreeHead = behaviourTree.References[subtreeHead];
        }
    }
}
