using Amlos.AI.Variables;
using System;
using UnityEngine;
namespace Amlos.AI
{
    [Serializable]
    public sealed class Divide : Arithmetic
    {
        [TypeExclude(VariableType.Bool, VariableType.String)]
        public VariableField a;

        [TypeExclude(VariableType.Bool, VariableType.String)]
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
                    result.Value = a.IntValue / b.IntValue;
                }
                else if (a.IsNumeric && b.IsNumeric) result.Value = a.NumericValue / b.NumericValue;
                else if (a.IsVector && b.IsNumeric) result.Value = a.VectorValue / b.NumericValue;
                else if (a.IsNumeric && b.IsVector)
                {
                    if (b.Type == VariableType.Vector3)
                    {
                        result.Value = new Vector3(a.NumericValue / b.Vector3Value.x,
                            a.NumericValue / b.Vector3Value.y,
                            a.NumericValue / b.Vector3Value.z);
                    }
                    else if (b.Type == VariableType.Vector2)
                    {
                        result.Value = new Vector2(a.NumericValue / b.Vector3Value.x,
                            a.NumericValue / b.Vector3Value.y);
                    }
                }
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
