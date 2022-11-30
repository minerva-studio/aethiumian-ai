using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// Call the method every frame
    /// </summary>
    [Serializable]
    public sealed class ScriptAction : Action
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
                method.Invoke(behaviourTree.Script, Parameter.ToValueArray(this, parameters));
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
    }
}