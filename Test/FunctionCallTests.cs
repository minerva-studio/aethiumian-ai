using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using NUnit.Framework;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Tests
{
    public sealed class FunctionCallTests
    {
        [AIFunction("Tests", "Registered Add")]
        public static int RegisteredAdd(int a, int b) => a + b;

        public static Task<bool> ReturnTask() => Task.FromResult(true);

        public int InstanceAdd(int a, int b) => a + b;

        [AIFunction(displayName: "Registered Instance")]
        public int RegisteredInstanceAdd(int a, int b) => a + b;

        public static int UnregisteredStatic(int value) => value;

        [Test]
        public void AIFunctionAttribute_RegistersCustomStaticMethod()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry.GetCustomFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(RegisteredAdd));

            Assert.NotNull(candidate);
            Assert.AreEqual("Global/Tests", candidate.Path);
            Assert.AreEqual("Registered Add", candidate.DisplayName);
            Assert.AreEqual(FunctionRegistry.ReceiverAssignment.None, candidate.ReceiverAssignment);
        }

        [Test]
        public void FunctionReference_RoundTripsMethodSignature()
        {
            MethodInfo method = typeof(FunctionCallTests).GetMethod(nameof(RegisteredAdd));
            FunctionReference reference = new();

            reference.SetMethod(method);
            MethodInfo resolved = FunctionRegistry.Resolve(reference);

            Assert.AreEqual(method, resolved);
            Assert.AreEqual(2, reference.parameterTypeNames.Count);
        }

        [Test]
        public void FunctionRegistry_CustomIdResolvesRegisteredMethod()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry.GetCustomFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(RegisteredAdd));
            FunctionReference reference = new();

            Assert.NotNull(candidate);
            reference.SetMethod(candidate.Method, candidate.CustomId);

            Assert.AreEqual(candidate.Method, FunctionRegistry.Resolve(reference));
        }

        [Test]
        public void FunctionRegistry_FallsBackToMethodIdentityWhenCustomIdIsMissing()
        {
            MethodInfo method = typeof(FunctionCallTests).GetMethod(nameof(RegisteredAdd));
            FunctionReference reference = new();

            reference.SetMethod(method, "missing-custom-id");

            Assert.AreEqual(method, FunctionRegistry.Resolve(reference));
        }

        [Test]
        public void FunctionRegistry_ReportsAwaitableReturn()
        {
            MethodInfo method = typeof(FunctionCallTests).GetMethod(nameof(ReturnTask));

            Assert.True(FunctionRegistry.IsAwaitableReturn(method.ReturnType));
        }

        [Test]
        public void FunctionRegistry_ProvidesArithmeticCandidates()
        {
            Assert.True(FunctionRegistry.GetArithmeticFunctions().Any(candidate => candidate.Method.Name == nameof(FunctionArithmetic.Add)));
        }

        [Test]
        public void FunctionRegistry_GlobalOnlyContainsRegisteredStaticMethods()
        {
            Assert.False(FunctionRegistry.GetCustomFunctions().Any(candidate => candidate.Method.Name == nameof(UnregisteredStatic)));
        }

        [Test]
        public void FunctionRegistry_ObjectCategoryRequiresReceiverType()
        {
            Assert.False(FunctionRegistry.GetMethods(null, BindingFlags.Public | BindingFlags.Instance, null, FunctionRegistry.ReceiverAssignment.Preserve, includeUnregisteredFolder: true).Any());
        }

        [Test]
        public void FunctionRegistry_UnregisteredCandidateUsesContextSubfolder()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Instance,
                    "Object",
                    FunctionRegistry.ReceiverAssignment.Preserve,
                    includeUnregisteredFolder: true)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(InstanceAdd));

            Assert.NotNull(candidate);
            Assert.AreEqual(typeof(FunctionCallTests), candidate.ReceiverType);
            Assert.AreEqual("Object/Unregistered", candidate.Path);
            Assert.True(FunctionRegistry.FormatSignature(candidate.Method, candidate.GetDisplayReceiverType()).Contains("receiver: FunctionCallTests"));
        }

        [Test]
        public void FunctionRegistry_RegisteredInstanceCandidateDisplaysReceiver()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Instance,
                    "Target Script",
                    FunctionRegistry.ReceiverAssignment.TargetScript,
                    includeUnregisteredFolder: true)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(RegisteredInstanceAdd));

            Assert.NotNull(candidate);
            Assert.True(candidate.IsRegistered);
            Assert.AreEqual("Target Script", candidate.Path);
            Assert.AreEqual("Registered Instance", candidate.DisplayName);
            Assert.AreEqual(typeof(FunctionCallTests), candidate.GetDisplayReceiverType());
        }

        [Test]
        public void FunctionRegistry_TargetScriptUnregisteredCandidateUsesContextSubfolder()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Instance,
                    "Target Script",
                    FunctionRegistry.ReceiverAssignment.TargetScript,
                    includeUnregisteredFolder: true)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(InstanceAdd));

            Assert.NotNull(candidate);
            Assert.AreEqual(FunctionRegistry.ReceiverAssignment.TargetScript, candidate.ReceiverAssignment);
            Assert.AreEqual("Target Script/Unregistered", candidate.Path);
        }

        [Test]
        public void FunctionRegistry_GameObjectAndTransformCandidatesStayAtContextRoot()
        {
            FunctionRegistry.FunctionCandidate gameObjectCandidate = FunctionRegistry
                .GetMethods(
                    typeof(GameObject),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(GameObject.CompareTag));

            FunctionRegistry.FunctionCandidate transformCandidate = FunctionRegistry
                .GetMethods(
                    typeof(Transform),
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "Transform",
                    FunctionRegistry.ReceiverAssignment.Transform,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(Transform.SetParent));

            Assert.NotNull(gameObjectCandidate);
            Assert.AreEqual("GameObject", gameObjectCandidate.Path);
            Assert.NotNull(transformCandidate);
            Assert.AreEqual("Transform", transformCandidate.Path);
        }

        [Test]
        public void FunctionRegistry_DetectsBuiltInReceiverReferencesBySourceId()
        {
            VariableReference receiver = new();

            Assert.False(FunctionRegistry.IsBuiltInReceiverReference(receiver));

            receiver.SetReference(VariableData.GetGameObjectVariable());
            Assert.True(FunctionRegistry.IsBuiltInReceiverReference(receiver));

            receiver.SetReference(VariableData.GetTransformVariable());
            Assert.True(FunctionRegistry.IsBuiltInReceiverReference(receiver));

            receiver.SetReference(VariableData.GetTargetScriptVariable(typeof(FunctionCallTests)));
            Assert.True(FunctionRegistry.IsBuiltInReceiverReference(receiver));

            receiver.SetReference(new VariableData("Manual GameObject", VariableType.UnityObject));
            Assert.False(FunctionRegistry.IsBuiltInReceiverReference(receiver));
        }

        [Test]
        public void FunctionRegistry_AssignsBuiltInReceiverResourceForContextSources()
        {
            FunctionReference reference = new();

            reference.SetMethod(typeof(FunctionCallTests).GetMethod(nameof(InstanceAdd)));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.TargetScript, typeof(FunctionCallTests));
            Assert.AreEqual(VariableData.targetScript, reference.targetObject.UUID);

            reference.SetMethod(typeof(GameObject).GetMethod(nameof(GameObject.CompareTag), new[] { typeof(string) }));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.GameObject);
            Assert.AreEqual(VariableData.localGameObject, reference.targetObject.UUID);

            reference.SetMethod(typeof(Transform).GetMethod(nameof(Transform.DetachChildren), System.Type.EmptyTypes));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.Transform);
            Assert.AreEqual(VariableData.localTransform, reference.targetObject.UUID);
        }

        [Test]
        public void FunctionRegistry_ClearsReceiverResourceForStaticSources()
        {
            FunctionReference reference = new();
            reference.targetObject.SetReference(new VariableData("Manual Receiver", VariableType.UnityObject));

            reference.SetMethod(typeof(FunctionCallTests).GetMethod(nameof(RegisteredAdd)));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.None);
            Assert.False(reference.targetObject.HasEditorReference);

            reference.targetObject.SetReference(new VariableData("Manual Receiver", VariableType.UnityObject));
            reference.SetMethod(typeof(FunctionArithmetic).GetMethod(nameof(FunctionArithmetic.Add)));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.None);
            Assert.False(reference.targetObject.HasEditorReference);
        }

        [Test]
        public void FunctionRegistry_PreservesReceiverResourceForObjectSource()
        {
            VariableData manualReceiver = new("Manual Receiver", VariableType.UnityObject);
            FunctionReference reference = new();
            reference.targetObject.SetReference(manualReceiver);

            reference.SetMethod(typeof(FunctionCallTests).GetMethod(nameof(InstanceAdd)));
            FunctionRegistry.AssignReceiverResource(reference, FunctionRegistry.ReceiverAssignment.Preserve);
            Assert.AreEqual(manualReceiver.UUID, reference.targetObject.UUID);
        }
    }
}
