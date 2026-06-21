using Aethiumian.AI.References;
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
        public FunctionReference function = new();
        public List<Parameter> parameters = new();
        public VariableReference result = new();

        public override void Initialize()
        {
            InitializeReference(function?.targetObject);
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

        private void HandleReturnValue(MethodInfo method, object returnValue)
        {
            // Awaitable return values own completion. NodeProgress methods must call End themselves.
            if (returnValue is Task task)
            {
                EndAfter(task);
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

            object target = function.targetObject?.Value;
            if (target == null)
            {
                throw new InvalidOperationException("Function receiver is not assigned.");
            }

            return VariableUtility.ImplicitConversion(method.DeclaringType, target);
        }

        private void InitializeReference(VariableReference reference)
        {
            if (reference == null || reference.IsConstant)
            {
                return;
            }

            bool hasVariable = behaviourTree.TryGetVariable(reference.UUID, out Variable variable);
            reference.SetRuntimeReference(hasVariable ? variable : null);
        }

        private void EndAfter(IEnumerator enumerator)
        {
            TaskCompletionSource<bool> coroutineTask = new();
            EndAfter(coroutineTask.Task);

            if (behaviourTree?.AIComponent == null)
            {
                RunEnumeratorSynchronously(enumerator, coroutineTask);
                return;
            }

            AIComponent.StartCoroutine(Do());
            IEnumerator Do()
            {
                yield return enumerator;
                coroutineTask.TrySetResult(true);
            }
        }

        private static void RunEnumeratorSynchronously(IEnumerator enumerator, TaskCompletionSource<bool> coroutineTask)
        {
            try
            {
                while (enumerator.MoveNext())
                {
                }

                coroutineTask.TrySetResult(true);
            }
            catch (Exception e)
            {
                coroutineTask.TrySetException(e);
            }
        }

        private async void EndAfter(Task task)
        {
            try
            {
                await BeforeTimeout(task);
                if (!task.IsCompleted)
                {
                    return;
                }

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
            catch (Exception e)
            {
                Debug.LogException(e);
                Exception(e);
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
