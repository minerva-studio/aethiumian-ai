using System;

namespace Amlos.AI
{
    [NodeTip("Do node subtraction")]
    [Serializable]
    public class Subtract : Arithmetic
    {
        public VariableField a;
        public VariableField b;
        public VariableReference result;

        public override void Execute()
        {
            if (a.Type == VariableType.Bool || b.Type == VariableType.Bool)
            {
                End(false);
                return;
            }
            if (a.Type == VariableType.String || b.Type == VariableType.String)
            {
                End(false);
                return;
            }
            try
            {
                if (b.Type == VariableType.Int && a.Type == VariableType.Int)
                {
                    result.Value = a.IntValue - b.IntValue;
                }
                else result.Value = a.NumericValue - b.NumericValue;
                End(true);

            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }
        }
    }

}
