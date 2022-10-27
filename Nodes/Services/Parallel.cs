using System;

namespace Amlos.AI
{
    [NodeTip("Run a parallel subtree")]
    [Serializable]
    public class Parallel : Service
    {
        public NodeReference subtreeHead;

        public override void Execute()
        {
            SetNextExecute(subtreeHead);
        }

        public override void Initialize()
        {
            subtreeHead = behaviourTree.References[subtreeHead];
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
