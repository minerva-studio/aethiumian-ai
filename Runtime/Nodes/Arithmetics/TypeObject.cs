using Amlos.AI.References;
using Amlos.AI.Variables;

namespace Amlos.AI.Nodes
{
    [NodeTip("Store a type object in a variable")]
    public sealed class TypeObject : Arithmetic
    {
        public GenericTypeReference typeReference;
        public VariableReference result;

        public override State Execute()
        {
            if (!typeReference.HasReferType)
            {
                return HandleException(InvalidNodeException.VariableIsRequired(nameof(typeReference)));
            }
            if (result.HasValue)
            {
                result.SetValue(typeReference.ReferType);
            }
            return State.Success;
        }
    }
}