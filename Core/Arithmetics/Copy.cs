﻿using System;

namespace Amlos.AI
{
    [Serializable]
    [NodeTip("Copy value of one variable to another")]
    public class Copy : Arithmetic
    {
        public VariableField from;
        public VariableReference to;

        public override void Execute()
        {
            try
            {
                switch (to.Type)
                {
                    case VariableType.String:
                        to.Value = from.StringValue;
                        break;
                    case VariableType.Int:
                        to.Value = from.IntValue;
                        break;
                    case VariableType.Float:
                        to.Value = from.FloatValue;
                        break;
                    case VariableType.Bool:
                        to.Value = from.BoolValue;
                        break;
                }
                End(true);
            }
            catch (Exception)
            {
                End(false);
            }
        }
    }

}
