using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    public sealed class FunctionActionTests
    {
        public sealed class Receiver
        {
            public Task<int> ReturnTaskInt(int value) => Task.FromResult(value);
        }

        public sealed class ComponentReceiver : MonoBehaviour
        {
            public Task<int> ReturnTaskInt(int value) => Task.FromResult(value);
        }

        public static Task<bool> ReturnTaskBool(bool value) => Task.FromResult(value);

        public static Task<int> ReturnTaskInt(int value) => Task.FromResult(value);

        public static IEnumerator ReturnCoroutine()
        {
            yield break;
        }

        public static void CompleteWithProgress(NodeProgress progress, bool value)
        {
            progress.End(value);
        }

        [Test]
        public async Task Execute_TaskBool_UsesBoolAsNodeResult()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnTaskBool), new Parameter(false));

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Failed));
        }

        [Test]
        public async Task Execute_TaskWithValue_WritesResult()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnTaskInt), new Parameter(42));
            TreeVariable resultVariable = SetResult(action, VariableType.Int);

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
            Assert.That(resultVariable.intValue, Is.EqualTo(42));
        }

        [Test]
        public async Task Execute_Coroutine_CompletesSuccessfully()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnCoroutine));

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
        }

        [Test]
        public async Task Execute_NodeProgressMethod_UsesManualEnd()
        {
            FunctionAction action = CreateStaticAction(
                nameof(CompleteWithProgress),
                new Parameter(VariableType.Node),
                new Parameter(true));

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
        }

        [Test]
        public void ObjectAction_UpgradesToFunctionAction_WhenActionCapable()
        {
            ObjectAction action = new()
            {
                type = typeof(Receiver),
                @object = new VariableReference(),
                MethodName = nameof(Receiver.ReturnTaskInt),
                Parameters = new List<Parameter> { new(5) },
                result = new VariableReference(),
            };

            TreeNode upgraded = action.Upgrade();

            Assert.That(upgraded, Is.TypeOf<FunctionAction>());
            FunctionAction functionAction = (FunctionAction)upgraded;
            Assert.That(functionAction.function.methodName, Is.EqualTo(nameof(Receiver.ReturnTaskInt)));
            Assert.That(functionAction.targetObject, Is.SameAs(action.@object));
            Assert.That(functionAction.parameters, Is.SameAs(action.Parameters));
            Assert.That(functionAction.result, Is.SameAs(action.result));
        }

        [Test]
        public void ComponentAction_GetComponent_UpgradesToFunctionActionWithGameObjectReceiver()
        {
            ComponentAction action = new()
            {
                getComponent = true,
                type = typeof(ComponentReceiver),
                MethodName = nameof(ComponentReceiver.ReturnTaskInt),
                Parameters = new List<Parameter> { new(5) },
                result = new VariableReference(),
            };

            TreeNode upgraded = action.Upgrade();

            Assert.That(upgraded, Is.TypeOf<FunctionAction>());
            FunctionAction functionAction = (FunctionAction)upgraded;
            Assert.That(functionAction.function.methodName, Is.EqualTo(nameof(ComponentReceiver.ReturnTaskInt)));
            Assert.That(functionAction.targetObject.UUID, Is.EqualTo(VariableData.localGameObject));
        }

        [Test]
        public void ObjectAction_RepeatMode_DoesNotOfferFunctionActionUpgrade()
        {
            ObjectAction action = new()
            {
                type = typeof(Receiver),
                @object = new VariableReference(),
                MethodName = nameof(Receiver.ReturnTaskInt),
                Parameters = new List<Parameter> { new(5) },
                actionCallTime = ObjectActionBase.ActionCallTime.update,
            };

            Assert.False(action.CanUpgrade());
            Assert.IsNull(action.Upgrade());
        }

        private static FunctionAction CreateStaticAction(string methodName, params Parameter[] parameters)
        {
            MethodInfo method = typeof(FunctionActionTests).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            FunctionAction action = new()
            {
                parameters = parameters.ToList(),
            };
            action.function.SetMethod(method);
            return action;
        }

        private static async Task<State> Execute(FunctionAction action)
        {
            State initialState = action.Execute();
            if (initialState != State.WaitAction)
            {
                return initialState;
            }

            return await action.ActionTask;
        }

        private static TreeVariable SetResult(FunctionAction action, VariableType type)
        {
            VariableData data = new("Result", type)
            {
                DefaultValue = GetDefaultValue(type),
            };
            TreeVariable variable = new(data);
            action.result = new VariableReference();
            action.result.SetRuntimeReference(variable);
            return variable;
        }

        private static string GetDefaultValue(VariableType type)
        {
            return type switch
            {
                VariableType.Int => "0",
                VariableType.Float => "0",
                VariableType.Bool => "false",
                VariableType.String => string.Empty,
                _ => string.Empty,
            };
        }
    }
}
