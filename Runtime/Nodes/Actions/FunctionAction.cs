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
        private Type pendingReturnType;
        [NonSerialized]
        private Coroutine pendingCoroutine;

        public FunctionReference function = new();
        [Readable]
        public VariableReference targetObject = new();
        [Readable]
        public List<Parameter> parameters = new();
        [Writable]
        public VariableReference result = new();
        public ReturnMode returnMode = ReturnMode.Default;

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
            pendingReturnType = null;
        }

        private void HandleReturnValue(MethodInfo method, object returnValue)
        {
            // Awaitable return values own completion. NodeProgress methods must call End themselves.
            if (returnValue is Task task)
            {
                pendingReturnType = method.ReturnType;
                pendingTask = task;
                CompletePendingTask();
                return;
            }

            if (returnValue is IEnumerator enumerator)
            {
                EndAfter(enumerator, method.ReturnType);
                return;
            }

#if UNITY_2023_1_OR_NEWER
            if (returnValue is Awaitable awaitable)
            {
                EndAfter(awaitable, method.ReturnType);
                return;
            }

            Type returnType = method.ReturnType;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Awaitable<>))
            {
                EndAfter(AwaitableToTask(returnValue), method.ReturnType);
                return;
            }
#endif

            if (result?.HasReference == true && method.ReturnType != typeof(void))
            {
                result.SetValue(returnValue);
            }

            if (!FunctionRegistry.HasNodeProgressParameter(method))
            {
                End(FunctionResultUtility.Resolve(returnMode, method.ReturnType, returnValue));
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

        private void EndAfter(IEnumerator enumerator, Type returnType)
        {
            if (behaviourTree?.AIComponent == null)
            {
                RunEnumeratorSynchronously(enumerator, returnType);
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
                End(FunctionResultUtility.Resolve(returnMode, returnType, null));
            }
        }

        private void RunEnumeratorSynchronously(IEnumerator enumerator, Type returnType)
        {
            try
            {
                while (enumerator.MoveNext())
                {
                }

                End(FunctionResultUtility.Resolve(returnMode, returnType, null));
            }
            catch (Exception e)
            {
                Exception(e);
            }
        }

        private void EndAfter(Task task, Type returnType)
        {
            pendingReturnType = returnType;
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
            Type returnType = pendingReturnType;
            pendingReturnType = null;
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
                    End(FunctionResultUtility.Resolve(returnMode, returnType, returnValue));
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

        private void EndAfter(Awaitable awaitable, Type returnType)
        {
            EndAfter(AwaitableToTask(awaitable), returnType);
            CancellationToken.Register(static state => ((Awaitable)state).Cancel(), awaitable);
        }
#endif

        /// <summary>Maps NodeProgress completion according to this FunctionAction's result mode.</summary>
        protected override bool ResolveExternalCompletion(bool @return)
        {
            if (returnMode != ReturnMode.Default)
            {
                Debug.LogWarning($"FunctionAction [{name}] completed through NodeProgress before its function return value.");
            }

            return returnMode switch
            {
                ReturnMode.AlwaysSuccess => true,
                ReturnMode.AlwaysFailure => false,
                _ => @return,
            };
        }
    }
}
