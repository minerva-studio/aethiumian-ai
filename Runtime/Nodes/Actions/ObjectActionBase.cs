using Amlos.AI.Variables;
using Minerva.Module.Tasks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
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
#if UNITY_2023_1_OR_NEWER
                // return value is Awaitable
                if (result is Awaitable awaitable)
                {
                    EndAfter(awaitable);
                    return;
                }
                // return value is Awaitable
                if (result is Awaitable<bool> awaitableb)
                {
                    EndAfter(awaitableb);
                    return;
                }
#endif
            }

            if (Result.HasReference) Result.SetValue(result);
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

        private void EndAfter(IEnumerator enumerator)
        {
            TaskCompletionSource<bool> coroutineTask = new();
            AIComponent.StartCoroutine(Do());
            EndAfter(coroutineTask.Task);
            IEnumerator Do()
            {
                yield return enumerator;
                coroutineTask.SetResult(true);
            }
        }

        protected async void EndAfter(Task task)
        {
            try
            {
                await BeforeTimeout(task);
                // timeout, let AI component to handle the rest
                if (!task.IsCompleted) return;
                // the running task itself is cancelled
                // just fail the action
                if (task.IsCanceled)
                {
                    Fail();
                }
                else if (task.IsFaulted)
                {
                    Exception(task.Exception);
                    return;
                }
                // has result
                else
                {
                    object result = GetReturnedValue(task);
                    if (Result.HasReference) Result.SetValue(result);
                    End(result is not bool b || b);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Exception(e);
            }
        }

        protected async void EndAfter<T>(Task<T> task)
        {
            try
            {
                await BeforeTimeout(task);
                // timeout, let AI component to handle the rest
                if (!task.IsCompleted) return;
                // the running task itself is cancelled
                // just fail the action
                if (task.IsCanceled)
                {
                    Fail();
                }
                else if (task.IsFaulted)
                {
                    Exception(task.Exception);
                    return;
                }
                // has result
                else
                {
                    var result = task.Result;
                    if (Result.HasReference) Result.SetValue(result);
                    End(result is not bool b || b);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Exception(e);
            }
        }

#if UNITY_2023_1_OR_NEWER
        public async Task AsTask(Awaitable awaitable) => await awaitable;

        public async Task<T> AsTask<T>(Awaitable<T> awaitable) => await awaitable;

        protected void EndAfter(Awaitable awaitable)
        {
            Task task = AsTask(awaitable);
            EndAfter(task);
            CancellationToken.Register(static (o) => ((Awaitable)o!).Cancel(), awaitable);
        }

        protected void EndAfter(Awaitable<bool> awaitable)
        {
            Task<bool> task = AsTask(awaitable);
            EndAfter(task);
            CancellationToken.Register(static (o) => ((Awaitable)o!).Cancel(), awaitable);
        }
#endif

        public abstract object Call();




        /// <summary>
        /// Get returned value from a task, if any
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        public static object GetReturnedValue(Task task)
        {
            if (!task.IsCompletedSuccessfully) return null;

            Type type = task.GetType();
            if (!type.IsGenericType) return null;

            // get generic task (with return value)
            var p = type.GetProperty(nameof(Task<int>.Result));
            var result = p.GetValue(task, null);
            return result;
        }
    }
}