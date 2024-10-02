using Amlos.AI.References;
using Amlos.AI.Utils;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Reflection;

namespace Amlos.AI.Nodes
{

    [Serializable]
    public sealed class ComponentAction : ObjectActionBase, IMethodCaller, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent = true;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;
        public TypeReference type;


        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public TypeReference TypeReference { get => type; }
        public VariableReference Component { get => component; set => component = value; }

        public override object Call()
        {
            Type referType = type.ReferType;
            var component = getComponent ? gameObject.GetComponent(referType) : this.component.Value;

            //var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            //var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();
            var method = MemberInfoCache.Instance.GetMethod(referType, MethodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            object ret = method.Invoke(component, Parameter.ToValueArray(this, method, Parameters));
            return ret;
        }
    }
}