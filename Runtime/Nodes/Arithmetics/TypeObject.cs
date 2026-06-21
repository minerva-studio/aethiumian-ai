using Aethiumian.AI.References;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Store a type object in a variable")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class TypeObject : Arithmetic
    {
        public GenericTypeReference typeReference;
        [Writable]
        public VariableReference result;

        public override State Execute()
        {
            if (!typeReference.HasReferType)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(typeReference), this));
            }
            if (result.HasValue)
            {
                result.SetValue(typeReference.ReferType);
            }
            return State.Success;
        }
    }
}