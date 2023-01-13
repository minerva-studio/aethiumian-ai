using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
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

        public override void Call()
        {
            try
            {
                Type referType = type.ReferType;
                var component = getComponent ? gameObject.GetComponent(referType) : this.component.Value;

                var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();

                object ret = method.Invoke(component, Parameter.ToValueArray(this, method, Parameters));
                if (Result.HasReference) Result.Value = ret;
                //Debug.Log(ret);

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + MethodName + $" in class {type.ReferType.Name} cannot be invoke!"));
                End(false);
                return;
            }

            ActionEnd();
        }
    }
}