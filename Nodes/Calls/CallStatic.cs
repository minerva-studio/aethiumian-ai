using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Nodes
{
    [Alias("Static Call")]
    [Serializable]
    public sealed class CallStatic : Call, IMethodCaller, IGenericMethodCaller
    {
        public TypeReference type;
        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }
        public TypeReference TypeReference => type;

        public override void Execute()
        {
            object ret;
            try
            {
                var methods = type.ReferType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
                ret = method.Invoke(null, Parameter.ToValueArray(this, method, Parameters));
                Log(ret);
            }
            catch (Exception e)
            {
                LogException(e);
                LogException(new ArithmeticException("Method " + MethodName + $" in class {type.ReferType.Name} cannot be invoke!"));
                End(false);
                return;
            }

            if (Result.HasReference)
            {
                Result.Value = ret;
            }

            //no return
            if (ret is null)
            {
                End(true);
                return;
            }
            else if (ret is bool b)
            {
                End(b);
                return;
            }
            else End(true);
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }
}