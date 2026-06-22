using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

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

        public static Task ReturnTask() => Task.CompletedTask;

        public static Task<bool> ReturnTaskBool(bool value) => Task.FromResult(value);

        public static Task<int> ReturnTaskInt(int value) => Task.FromResult(value);

#if UNITY_2023_1_OR_NEWER
#pragma warning disable CS1998
        public static async Awaitable ReturnAwaitable()
        {
        }

        public static async Awaitable<int> ReturnAwaitableInt(int value)
        {
            return value;
        }
#pragma warning restore CS1998
#endif

        public static IEnumerator ReturnCoroutine()
        {
            yield break;
        }

        public static void CompleteWithProgress(NodeProgress progress, bool value)
        {
            progress.End(value);
        }

        [Test]
        public void Duplicate_FunctionAction_DeepCopiesCallableFields()
        {
            FunctionAction source = CreateStaticAction(nameof(ReturnTaskInt), new Parameter(5));
            VariableData targetData = new("Target", VariableType.UnityObject);
            VariableData resultData = new("Result", VariableType.Int);
            source.targetObject.SetReference(targetData);
            source.result.SetReference(resultData);

            FunctionAction clone = (FunctionAction)NodeFactory.Duplicate(source);

            AssertCallableFieldsEquivalent(source, clone);
            clone.parameters[0] = new Parameter(9);
            clone.function.parameterTypeNames.Add(typeof(string).FullName);
            clone.targetObject.SetReference(new VariableData("Other Target", VariableType.UnityObject));

            Assert.That(source.parameters[0].IntValue, Is.EqualTo(5));
            Assert.That(source.function.parameterTypeNames, Has.Count.EqualTo(1));
            Assert.That(source.targetObject.UUID, Is.EqualTo(targetData.UUID));
        }

        [Test]
        public void Copy_FunctionAction_CopiesGeneratedNodeDataAndDeepCopiesCallableFields()
        {
            FunctionAction source = CreateStaticAction(nameof(ReturnTaskInt), new Parameter(5));
            source.name = "Source Function Action";
            source.uuid = UUID.NewUUID();
            source.parent = new NodeReference(UUID.NewUUID());
            VariableData targetData = new("Target", VariableType.UnityObject);
            VariableData resultData = new("Result", VariableType.Int);
            source.targetObject.SetReference(targetData);
            source.result.SetReference(resultData);

            FunctionAction destination = TreeTestFixture.CreateNode<FunctionAction>("Destination");

            NodeFactory.Copy(destination, source);

            Assert.That(destination.name, Is.EqualTo(source.name));
            Assert.That(destination.uuid, Is.EqualTo(source.uuid));
            Assert.That(destination.parent, Is.Not.SameAs(source.parent));
            Assert.That(destination.parent.UUID, Is.EqualTo(source.parent.UUID));
            AssertCallableFieldsEquivalent(source, destination);
            destination.parameters[0] = new Parameter(11);
            destination.parent.UUID = UUID.NewUUID();

            Assert.That(source.parameters[0].IntValue, Is.EqualTo(5));
            Assert.That(source.parent.UUID, Is.Not.EqualTo(destination.parent.UUID));
        }

        [Test]
        public async Task Execute_Task_CompletesSuccessfully()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnTask));

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
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

#if UNITY_2023_1_OR_NEWER
        [Test]
        public async Task Execute_Awaitable_CompletesSuccessfully()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnAwaitable));

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
        }

        [Test]
        public async Task Execute_AwaitableWithValue_WritesResult()
        {
            FunctionAction action = CreateStaticAction(nameof(ReturnAwaitableInt), new Parameter(77));
            TreeVariable resultVariable = SetResult(action, VariableType.Int);

            State state = await Execute(action);

            Assert.That(state, Is.EqualTo(State.Success));
            Assert.That(resultVariable.intValue, Is.EqualTo(77));
        }
#endif

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

        [UnityTest]
        public IEnumerator BehaviourTree_FunctionActionRuntimeCopy_LinksResultAndExecutes()
        {
            VariableData resultData = new("Result", VariableType.Int)
            {
                DefaultValue = "0",
            };
            FunctionAction prototype = TreeTestFixture.CreateNode<FunctionAction>("Runtime Function Action");
            ConfigureStaticAction(prototype, nameof(ReturnTaskInt), new Parameter(64));
            prototype.result.SetReference(resultData);

            using TreeTestFixture fixture = TreeTestFixture.Create(prototype, new[] { resultData });
            yield return fixture.WaitUntilReady();
            FunctionAction runtimeAction = fixture.GetRuntimeNode(prototype);

            Assert.That(runtimeAction, Is.Not.SameAs(prototype));
            Assert.That(runtimeAction.Prototype, Is.SameAs(prototype));
            AssertCallableFieldsEquivalent(prototype, runtimeAction);
            Assert.That(runtimeAction.result.HasReference, Is.True);

            fixture.Start();
            yield return fixture.WaitUntil(() => runtimeAction.result.IntValue == 64 || fixture.Tree.MainStack.State == BehaviourTree.NodeCallStack.StackState.End);

            Assert.That(runtimeAction.result.IntValue, Is.EqualTo(64));
            Assert.That(fixture.Tree.MainStack.State, Is.EqualTo(BehaviourTree.NodeCallStack.StackState.End));
            Assert.That(fixture.Tree.MainStack.Exception, Is.Null);
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
            FunctionAction action = new()
            {
                parameters = parameters.ToList(),
            };
            ConfigureStaticAction(action, methodName, parameters);
            return action;
        }

        private static void ConfigureStaticAction(FunctionAction action, string methodName, params Parameter[] parameters)
        {
            MethodInfo method = typeof(FunctionActionTests).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Missing static test method {methodName}.");
            action.parameters = parameters.ToList();
            action.function.SetMethod(method);
        }

        private static async Task<State> Execute(FunctionAction action)
        {
            State initialState = action.Execute();
            if (initialState != State.WaitAction)
            {
                return initialState;
            }

            Task completed = await Task.WhenAny(action.ActionTask, Task.Delay(1000));
            if (completed != action.ActionTask)
            {
                action.Stop();
                Assert.Fail($"FunctionAction did not complete within timeout. Method: {action.function.methodName}");
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

        private static void AssertCallableFieldsEquivalent(FunctionAction source, FunctionAction clone)
        {
            Assert.That(clone.function, Is.Not.SameAs(source.function));
            Assert.That(clone.function.declaringTypeFullName, Is.EqualTo(source.function.declaringTypeFullName));
            Assert.That(clone.function.declaringAssemblyName, Is.EqualTo(source.function.declaringAssemblyName));
            Assert.That(clone.function.methodName, Is.EqualTo(source.function.methodName));
            Assert.That(clone.function.parameterTypeNames, Is.EqualTo(source.function.parameterTypeNames));
            Assert.That(clone.function.parameterTypeNames, Is.Not.SameAs(source.function.parameterTypeNames));

            Assert.That(clone.parameters, Is.Not.SameAs(source.parameters));
            Assert.That(clone.parameters, Has.Count.EqualTo(source.parameters.Count));
            for (int i = 0; i < source.parameters.Count; i++)
            {
                Assert.That(clone.parameters[i], Is.Not.SameAs(source.parameters[i]));
                Assert.That(clone.parameters[i].Type, Is.EqualTo(source.parameters[i].Type));
                if (source.parameters[i].Type != VariableType.Node)
                {
                    Assert.That(clone.parameters[i].Value, Is.EqualTo(source.parameters[i].Value));
                }
            }

            Assert.That(clone.targetObject, Is.Not.SameAs(source.targetObject));
            Assert.That(clone.targetObject.UUID, Is.EqualTo(source.targetObject.UUID));
            Assert.That(clone.result, Is.Not.SameAs(source.result));
            Assert.That(clone.result.UUID, Is.EqualTo(source.result.UUID));
        }
    }
}
