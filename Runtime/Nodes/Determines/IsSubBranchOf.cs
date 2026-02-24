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
                var node = behaviourTree.GetNode(root);
                return node.IsParentOf(behaviourTree.CurrentStage.Node) || node == behaviourTree.CurrentStage.Node;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
    }
}
