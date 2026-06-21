using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    [NodeMenuPath("External")]
    [NodeTip("Call a method on the object")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class ObjectCall : ObjectCallBase, IGenericMethodCaller, IObjectCaller
    {
        public VariableReference @object;
        public GenericTypeReference type;


        public TypeReference TypeReference => type;
        public VariableReference Object { get => @object; set => @object = value; }

        public override State Execute()
        {
            Type referType = type.ReferType;
            object obj = @object.Value;
            return Call(obj, referType);
        }

#if UNITY_EDITOR
        public override TreeNode Upgrade()
        {
            MethodInfo method = type.ReferType?
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method => method.Name == MethodName && MethodCallers.ParameterMatches(method, Parameters));

            FunctionCall newNode = new()
            {
                parameters = Parameters,
                result = result,
            };
            newNode.function.targetObject = @object;
            newNode.function.SetMethod(method);
            return newNode;
        }
#endif
    }
}
