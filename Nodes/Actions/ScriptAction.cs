using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Call the method every frame
    /// </summary>
    [Serializable]
    public sealed class ScriptAction : Action, IMethodCaller
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


        private float counter;

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

        private void Call()
        {
            try
            {
                var method = behaviourTree.Script.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                var res = method.Invoke(behaviourTree.Script, Parameter.ToValueArray(this, parameters));
                if (result.HasRuntimeReference)
                {
                    result.Value = res;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogException(new ArithmeticException("Method " + methodName + $" in script {behaviourTree.Script.GetType().Name} cannot be invoke!"));
                End(false);
                return;
            }

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
            for (int i = 0; i < parameters.Count; i++)
            {
                Parameter item = parameters[i];
                parameters[i] = (Parameter)item.Clone();
                if (!parameters[i].IsConstant)
                {
                    bool hasVar = behaviourTree.Variables.TryGetValue(parameters[i].UUID, out Variable variable);
                    if (hasVar) parameters[i].SetRuntimeReference(variable);
                    else parameters[i].SetRuntimeReference(null);
                }
            }
        }
    }
}