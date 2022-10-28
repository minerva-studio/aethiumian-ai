using Minerva.Module;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amlos.AI
{


    [Serializable]
    public abstract class Service : TreeNode
    {
        public int interval;
        public RangeInt randomDeviation;

        public Service() : base()
        {

        }

        public sealed override void End(bool @return)
        {
            behaviourTree.EndService(this);
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
