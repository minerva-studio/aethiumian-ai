using Amlos.AI.References;

namespace Amlos.AI
{
    public sealed class IsSubBranchOf : Determine
    {
        public RawNodeReference root;

        public override bool GetValue()
        {
            try
            {
                return root.Node.IsParentOf(behaviourTree.CurrentStage) || root.Node == behaviourTree.CurrentStage;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}