using Amlos.AI.References;
using Amlos.AI.Utils;
using Amlos.AI.Variables;
using System;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Execute a method as an action on the object")]
    public sealed class ObjectAction : ObjectActionBase, IMethodCaller, IGenericMethodCaller, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference<Component> type;


        public TypeReference TypeReference => type;
        public VariableReference Object { get => @object; set => @object = value; }

        public override object Call()
        {
            Type referType = type.ReferType;
            var component = this.@object.Value;

            //var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            var method = MemberInfoCache.Instance.GetMethod(referType, MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            object ret = method.Invoke(component, Parameter.ToValueArray(this, method, Parameters, GetCancellationTokenSource));
            return ret;
        }
    }
}