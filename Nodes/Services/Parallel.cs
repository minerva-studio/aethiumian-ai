using System;

namespace Amlos.AI
{
    [NodeTip("Run a parallel subtree")]
    [Serializable]
    public sealed class Parallel : Service
    {
        public NodeReference subtreeHead;
        private bool isRunning;

        public override bool IsReady => !isRunning;

        public override void Execute()
        {
            isRunning = true;
            SetNextExecute(subtreeHead);
        }

        public override void Initialize()
        {
            subtreeHead = behaviourTree.References[subtreeHead];
        }

        public override void UpdateTimer()
        {
            //nothing
        }

        public override void OnRegistered()
        {
            isRunning = false;
        }

        public override void OnUnregistered()
        {
            isRunning = false;
        }
    }
    /**
     * - Sequence
     *   - store enemyCount from GetEnemyCount(); [Node]
     *   - condition
     *     - if enemyCount > 3
     *     - true: ()
     */
}
