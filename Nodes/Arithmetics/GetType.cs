namespace Amlos.AI
{
    [NodeTip("Store a type object in a variable")]
    public sealed class GetType : Arithmetic
    {
        public TypeReference TypeReference;
        public VariableReference result;

        public override void Execute()
        {
            if (result.HasRuntimeValue)
            {
                result.Value = TypeReference.ReferType;
            }
            End(true);
        }
    }
}