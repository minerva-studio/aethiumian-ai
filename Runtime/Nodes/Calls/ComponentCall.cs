using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Nodes
{
    [DoNotRelease]
    [Serializable]
    public sealed class ComponentCall : ObjectCallBase, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent = true;
        public GenericTypeReference type;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;


        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public TypeReference TypeReference => type;
        public VariableReference Component { get => component; set => component = value; }

        public override State Execute()
        {
            Type referType = type.ReferType;
            var obj = getComponent ? gameObject.GetComponent(referType) : this.component.Value;
            return Call(obj, referType);
        }


#if UNITY_EDITOR
        public override TreeNode Upgrade()
        {
            MethodInfo method = type.ReferType?
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method => method.Name == MethodName && MethodCallers.ParameterMatches(method, Parameters));

            var newNode = new FunctionCall()
            {
                parameters = this.Parameters,
                result = this.result,
            };
            newNode.function.targetObject = getComponent
                ? new VariableReference()
                : component;
            newNode.function.SetMethod(method);
            if (getComponent)
            {
                newNode.function.targetObject.SetReference(VariableData.GetGameObjectVariable());
            }

            return newNode;
        }
#endif
    }
}
