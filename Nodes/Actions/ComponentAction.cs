using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    public abstract class ComponentActionBase : Action, IMethodCaller
    {
        public enum UpdateEndType
        {
            byCounter,
            byTimer,
            byMethod
        }

        public enum ActionCallTime
        {
            fixedUpdate,
            update,
            once,
        }

        public string methodName;
        public List<Parameter> parameters;
        public VariableField<float> duration;
        public VariableField<int> count;
        public UpdateEndType endType;
        public ActionCallTime actionCallTime;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        protected float counter;

        public override void BeforeExecute()
        {
            counter = 0;
        }

        public override void ExecuteOnce()
        {
            if (actionCallTime == ActionCallTime.once) Call();
        }

        public override void Update()
        {
            if (actionCallTime == ActionCallTime.update) Call();
        }

        public override void FixedUpdate()
        {
            if (actionCallTime == ActionCallTime.fixedUpdate) Call();
        }

        public abstract void Call();

        public void ActionEnd()
        {
            switch (endType)
            {
                case UpdateEndType.byCounter:
                    counter++;
                    if (counter > count)
                    {
                        End(true);
                        return;
                    }
                    break;
                case UpdateEndType.byTimer:
                    counter += Time.deltaTime;
                    if (counter > duration)
                    {
                        End(true);
                        return;
                    }
                    break;
                case UpdateEndType.byMethod:
                default:
                    break;
            }
        }

        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }
    }

    [Serializable]
    public sealed class ComponentAction : ComponentActionBase, IMethodCaller, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;
        public TypeReference<Component> type;


        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public TypeReference TypeReference { get => type; }
        public VariableReference Component { get => component; set => component = value; }

        public override void Call()
        {
            try
            {
                Type referType = type.ReferType;
                var methods = referType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var method = methods.Where(m => m.Name == MethodName && MethodCallers.ParameterMatches(m, parameters)).FirstOrDefault();

                var component = getComponent ? gameObject.GetComponent(referType) : this.component.Value;
                object ret = method.Invoke(component, Parameter.ToValueArray(this, Parameters));
                if (Result.HasRuntimeReference) Result.Value = ret;
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