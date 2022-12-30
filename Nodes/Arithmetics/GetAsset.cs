namespace Amlos.AI
{
    public sealed class GetAsset : Arithmetic
    {
        public AssetReference assetReference;
        [TypeLimit(VariableType.UnityObject, VariableType.Generic)]
        public VariableReference result;


        public override void Execute()
        {
            if (result.HasRuntimeValue)
            {
                result.Value = assetReference.GetAsset();
            }
            End(true);
        }
    }
}