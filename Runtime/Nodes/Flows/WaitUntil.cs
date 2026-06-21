using Aethiumian.AI.References;

namespace Aethiumian.AI.Nodes
{
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public class WaitUntil : Flow
    {
        public NodeReference condition;

        public override State Execute()
        {
            if (behaviourTree.GetNode(condition) != null)
                return SetNextExecute(condition);
            return HandleException(InvalidNodeException.ReferenceIsRequired(nameof(condition), this));
        }

        public override State ReceiveReturnFromChild(bool @return)
        {
            if (@return)
            {
                return State.Success;
            }
            else
            {
                return State.Yield;
            }
        }

        public override void Initialize()
        {
        }

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            return condition?.HasEditorReference == true;
        }
    }
}
