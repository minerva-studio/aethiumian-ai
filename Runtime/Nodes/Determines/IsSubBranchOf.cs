using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Checks whether the current node is executing inside a referenced branch.")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
