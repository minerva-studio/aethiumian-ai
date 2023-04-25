using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    [NodeTip("Execute a method as an action on the object")]
    public sealed class ObjectAction : ObjectActionBase, IMethodCaller, IGenericMethodCaller, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference<Component> type;


        public TypeReference TypeReference { get => type; }
        public VariableReference Object { get => @object; set => @object = value; }

        public override void Call()
        {
            Type referType = type.ReferType;
            var component = this.@object.Value;

            var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();

            object ret = method.Invoke(component, Parameter.ToValueArray(this, method, Parameters));
            if (Result.HasReference) Result.Value = ret;

            ActionEnd();
        }
    }
}