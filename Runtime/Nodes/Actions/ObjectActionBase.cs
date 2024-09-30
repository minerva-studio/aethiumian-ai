using Amlos.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    public abstract class ObjectActionBase : Action, IMethodCaller
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
            [InspectorName("Once (Monobehaviour Action)")]
            once,
        }

        public string methodName;
        public List<Parameter> parameters;
        public VariableField<float> duration;
        public VariableField<int> count;
        public UpdateEndType endType = UpdateEndType.byMethod;
        public ActionCallTime actionCallTime = ActionCallTime.once;
        public VariableReference result;

        public List<Parameter> Parameters { get => parameters; set => parameters = value; }
        public VariableReference Result { get => result; set => result = value; }
        public string MethodName { get => methodName; set => methodName = value; }

        protected float counter;


        public ObjectActionBase()
        {
            endType = UpdateEndType.byMethod;
            actionCallTime = ActionCallTime.once;
        }



        public override void Initialize()
        {
            MethodCallers.InitializeParameters(behaviourTree, this);
        }

        public override void Awake()
        {
            counter = 0;
        }

        public override void Start()
        {
            if (actionCallTime == ActionCallTime.once)
            {
                ExecuteMethod();
            }
        }

        public override void Update()
        {
            if (actionCallTime == ActionCallTime.update)
            {
                ExecuteMethod();
            }
        }

        public override void FixedUpdate()
        {
            if (actionCallTime == ActionCallTime.fixedUpdate)
            {
                ExecuteMethod();
            }
        }




        private void ExecuteMethod()
        {
            var result = Call();
            // function action
            if (endType == UpdateEndType.byMethod && actionCallTime == ActionCallTime.once)
            {
                // return value is task
                if (result is Task task)
                {
                    EndAfter(task);
                    return;
                }
                // return value is coroutine
                if (result is IEnumerator enumerator)
                {
                    EndAfter(enumerator);
                    return;
                }
            }
            if (Result.HasReference) Result.SetValue(result);
            ActionEnd();
        }

        private async void EndAfter(IEnumerator enumerator)
        {
            AIComponent.StartCoroutine(Do());
            bool flag = false;
            await Task.Delay(TimeSpan.FromSeconds(behaviourTree.Prototype.actionMaximumDuration));
            if (!flag) Fail();

            IEnumerator Do()
            {
                yield return enumerator;
                flag = true;
                Success();
            }
        }

        protected async void EndAfter(Task task)
        {
            try
            {
                await task;

                object result = GetReturnedValue(task);
                if (Result.HasReference) Result.SetValue(result);
                if (result is bool b)
                {
                    End(b);
                }
                else Success();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                Fail();
            }
        }

        public abstract object Call();

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




        /// <summary>
        /// Get returned value from a task, if any
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private static object GetReturnedValue(Task task)
        {
            object result = null;
            Type type = task.GetType();
            if (!task.IsFaulted && !task.IsCanceled) return null;
            if (!type.IsGenericType) return null;

            // get generic task (with return value)
            var p = type.GetProperty(nameof(Task<int>.Result));
            result = p.GetValue(task, null);
            return result;
        }
    }
}