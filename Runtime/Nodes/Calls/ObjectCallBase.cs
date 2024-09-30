using Amlos.AI.Utils;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Nodes
{
    public abstract class ObjectCallBase : Call, IMethodCaller
    {
        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public string MethodName { get => methodName; set => methodName = value; }
        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result => result;

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }

        protected State Call(object obj, Type referType)
        {
            object ret;
            var method = MemberInfoCache.Instance.GetMethod(referType, MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            ret = method.Invoke(obj, Parameter.ToValueArray(this, method, Parameters));

            if (Result.HasReference) Result.SetValue(ret);

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
    }
}