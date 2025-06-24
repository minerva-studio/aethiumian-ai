using Amlos.AI.References;

namespace Amlos.AI.Nodes
{
    public sealed class IsSubBranchOf : Determine
    {
        public RawNodeReference root;

        public override bool GetValue()
        {
            try
            {
                return root.Node.IsParentOf(behaviourTree.CurrentStage.Node) || root.Node == behaviourTree.CurrentStage.Node;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}