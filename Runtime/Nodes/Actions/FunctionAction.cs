using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    [NodeMenuPath("External")]
    [NodeTip("Execute a selected function as an action")]
    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class FunctionAction : Action
    {
        [NonSerialized]
        private Task pendingTask;
        [NonSerialized]
        private Coroutine pendingCoroutine;

        public FunctionReference function = new();
        [Readable]
        public VariableReference targetObject = new();
        [Readable]
        public List<Parameter> parameters = new();
        [Writable]
        public VariableReference result = new();

        /// <summary>
        /// Stops and clears asynchronous sources left by a previous execution.
        /// </summary>
        public override void Awake()
        {
            ClearPendingSources();
        }

        public override void Start()
        {
            try
            {
                MethodInfo method = FunctionRegistry.Resolve(function);
                if (method == null || !FunctionRegistry.IsValidActionMethod(method))
                {
                    Fail();
                    return;
                }

                object target = ResolveInvokeTarget(method);
                parameters ??= new List<Parameter>();
                object[] values = Parameter.ToValueArray(this, method, parameters, GetCancellationTokenSource);
                object returnValue = method.Invoke(target, values);
                HandleReturnValue(method, returnValue);
            }
            catch (Exception e)
            {
                Exception(e);
            }
        }

        /// <summary>
        /// Polls an external task returned by the invoked function.
        /// </summary>
        public override void Update()
        {
            CompletePendingTask();
        }

        /// <summary>
        /// Clears asynchronous sources owned by this action instance.
        /// </summary>
        public override void OnDestroy()
        {
            ClearPendingSources();
        }

        /// <summary>
        /// Stops the owned coroutine and forgets the external task.
        /// </summary>
        private void ClearPendingSources()
        {
            if (pendingCoroutine != null && behaviourTree != null && behaviourTree.AIComponent != null)
            {
                behaviourTree.AIComponent.StopCoroutine(pendingCoroutine);
            }

            pendingCoroutine = null;
            pendingTask = null;
        }

        private void HandleReturnValue(MethodInfo method, object returnValue)
        {
            // Awaitable return values own completion. NodeProgress methods must call End themselves.
            if (returnValue is Task task)
            {
                pendingTask = task;
                CompletePendingTask();
                return;
            }

            if (returnValue is IEnumerator enumerator)
            {
                EndAfter(enumerator);
                return;
            }

#if UNITY_2023_1_OR_NEWER
            if (returnValue is Awaitable awaitable)
            {
                EndAfter(awaitable);
                return;
            }

            Type returnType = method.ReturnType;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Awaitable<>))
            {
                EndAfter(AwaitableToTask(returnValue));
                return;
            }
#endif

            if (result?.HasReference == true && method.ReturnType != typeof(void))
            {
                result.SetValue(returnValue);
            }

            if (!FunctionRegistry.HasNodeProgressParameter(method))
            {
                End(returnValue is not bool boolValue || boolValue);
            }
        }

        private object ResolveInvokeTarget(MethodInfo method)
        {
            if (method.IsStatic)
            {
                return null;
            }

            object target = targetObject?.Value;
            if (target == null)
            {
                throw new InvalidOperationException("Function receiver is not assigned.");
            }

            return VariableUtility.ImplicitConversion(method.DeclaringType, target);
        }

        private void EndAfter(IEnumerator enumerator)
        {
            if (behaviourTree?.AIComponent == null)
            {
                RunEnumeratorSynchronously(enumerator);
                return;
            }

            pendingCoroutine = AIComponent.StartCoroutine(Do());
            IEnumerator Do()
            {
                while (true)
                {
                    object yielded;
                    try
                    {
                        if (!enumerator.MoveNext())
                        {
                            break;
                        }

                        yielded = enumerator.Current;
                    }
                    catch (Exception exception)
                    {
                        pendingCoroutine = null;
                        Exception(exception);
                        yield break;
                    }

                    yield return yielded;
                }

                pendingCoroutine = null;
                End(true);
            }
        }

        private void RunEnumeratorSynchronously(IEnumerator enumerator)
        {
            try
            {
                while (enumerator.MoveNext())
                {
                }

                End(true);
            }
            catch (Exception e)
            {
                Exception(e);
            }
        }

        private void EndAfter(Task task)
        {
            pendingTask = task;
            CompletePendingTask();
        }

        /// <summary>
        /// Completes the action when its external task has finished.
        /// </summary>
        private void CompletePendingTask()
        {
            Task task = pendingTask;
            if (task == null || !task.IsCompleted || IsComplete)
            {
                return;
            }

            pendingTask = null;
            try
            {
                if (task.IsCanceled)
                {
                    Fail();
                }
                else if (task.IsFaulted)
                {
                    Exception(task.Exception);
                }
                else
                {
                    object returnValue = ObjectActionBase.GetReturnedValue(task);
                    if (result?.HasReference == true) result.SetValue(returnValue);
                    End(returnValue is not bool boolValue || boolValue);
                }
            }
            catch (Exception exception)
            {
                Exception(exception);
            }
        }

#if UNITY_2023_1_OR_NEWER
        private static async Task AwaitableToTask(Awaitable awaitable)
        {
            await awaitable;
        }

        private static Task AwaitableToTask(object awaitable)
        {
            Type awaitableType = awaitable.GetType();
            Type resultType = awaitableType.GetGenericArguments()[0];
            MethodInfo method = typeof(FunctionAction)
                .GetMethod(nameof(AwaitableWithResultToTask), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(resultType);
            return (Task)method.Invoke(null, new[] { awaitable });
        }

        private static async Task<T> AwaitableWithResultToTask<T>(Awaitable<T> awaitable)
        {
            return await awaitable;
        }

        private void EndAfter(Awaitable awaitable)
        {
            EndAfter(AwaitableToTask(awaitable));
            CancellationToken.Register(static state => ((Awaitable)state).Cancel(), awaitable);
        }
#endif
    }
}
