using Aethiumian.AI.Inspector;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    [DoNotRelease]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
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
                targetObject = getComponent
                    ? new VariableReference()
                    : component,
                parameters = this.Parameters,
                result = this.result,
            };
            newNode.function.SetMethod(method);
            if (getComponent)
            {
                newNode.targetObject.SetReference(VariableData.GetGameObjectVariable());
            }

            return newNode;
        }
#endif
    }
}
