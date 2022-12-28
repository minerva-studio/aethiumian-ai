using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public sealed class ComponentCall : Call, IMethodCaller, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent;
        public TypeReference<Component> type;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;

        public string methodName;
        public List<Parameter> parameters;
        public VariableReference result;

        public string MethodName { get => methodName; set => methodName = value; }
        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public TypeReference TypeReference { get => type; }
        public VariableReference Component { get => component; set => component = value; }

        public override void Execute()
        {
            object ret;
            try
            {
                Type referType = type.ReferType;
                var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();

                var component = getComponent ? gameObject.GetComponent(referType) : this.component.Value;
                ret = method.Invoke(component, Parameter.ToValueArray(this, Parameters));
                Debug.Log(ret);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + MethodName + $" in class {type.ReferType.Name} cannot be invoke!"));
                End(false);
                return;
            }

            if (Result.HasRuntimeReference)
            {
                Result.Value = ret;
            }

            //no return
            if (ret is null)
            {
                End(true);
                return;
            }
            else if (ret is bool b)
            {
                End(b);
                return;
            }
            else End(true);
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }
}