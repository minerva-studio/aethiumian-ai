using Aethiumian.AI.References;
using System;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Run a new branch as service of the current stack")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Branch : Service
    {
        public NodeReference subtreeHead;
        private bool isRunning;

        public override bool IsReady => !isRunning;

        public override State Execute()
        {
            isRunning = true;
            return SetNextExecute(subtreeHead);
        }

        public override void Initialize()
        {
            behaviourTree.GetNode(ref subtreeHead);
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
