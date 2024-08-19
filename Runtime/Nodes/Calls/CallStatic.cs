using Amlos.AI.References;
using Amlos.AI.Utils;
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

        private MethodInfo method;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }
        public TypeReference TypeReference => type;

        public override State Execute()
        {
            object ret;

            ret = method.Invoke(null, Parameter.ToValueArray(this, method, Parameters));
            if (Result.HasReference) Result.Value = ret;


            //no return
            if (ret is null)
            {
                return State.Success;
            }
            else if (ret is bool b)
            {
                return StateOf(b);
            }
            else return State.Success;
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
            //var methods = type.ReferType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            //method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            method = MemberInfoCache.Instance.GetMethod(type.ReferType, MethodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        }
    }
}