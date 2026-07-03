using Aethiumian.AI.Attributes;
using Aethiumian.AI.References;
using Aethiumian.AI.Utils;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aethiumian.AI.Nodes
{
    [DoNotRelease]
    [Serializable]
    [Obsolete]
    public sealed class ComponentAction : ObjectActionBase, IMethodCaller, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent = true;
        [DisplayIf(nameof(getComponent), false)]
        [Readable]
        public VariableReference component;
        public GenericTypeReference type;


        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public TypeReference TypeReference => type;
        public VariableReference Component { get => component; set => component = value; }

        public override object Call()
        {
            Type referType = type.ReferType;
            var component = getComponent ? gameObject.GetComponent(referType) : this.component.Value;

            //var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            var method = MemberInfoCache.Instance.GetMethod(referType, MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            object ret = method.Invoke(component, Parameter.ToValueArray(this, method, Parameters, GetCancellationTokenSource));
            return ret;
        }


#if UNITY_EDITOR
        public override TreeNode Upgrade()
        {
            List<Parameter> upgradeParameters = Parameters ?? new List<Parameter>();
            MethodInfo method = type?.ReferType?
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                .FirstOrDefault(method => method.Name == MethodName && MethodCallers.ParameterMatches(method, upgradeParameters));
            if (actionCallTime != ActionCallTime.once || !FunctionRegistry.IsValidActionMethod(method))
            {
                return null;
            }

            FunctionAction newNode = new()
            {
                targetObject = getComponent ? new VariableReference() : component,
                parameters = upgradeParameters,
                result = result,
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
