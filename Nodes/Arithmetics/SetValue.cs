using System;

namespace Amlos.AI
{
    [Serializable]
    public class SetValue : Arithmetic
    {
        VariableReference a;
        VariableField value;

        public override void Execute()
        {
            try
            {
                if (a.Type == VariableType.Int && value.Type == VariableType.Int)
                {
                    a.Value = value.IntValue;
                    End(true);
                }
                else if (a.Type == VariableType.Float && (value.Type == VariableType.Int || value.Type == VariableType.Float))
                {
                    a.Value = value.FloatValue;
                    End(true);
                }
                else if (a.Type == VariableType.Bool && value.Type == VariableType.Bool)
                {
                    a.Value = value.BoolValue;
                    End(true);
                }
                else if (a.Type == VariableType.String && value.Type == VariableType.String)
                {
                    a.Value = value.StringValue;
                    End(true);
                }
                else
                    End(false);
            }
            catch (System.Exception)
            {
                End(false);
                throw;
            }

        }
    }
}