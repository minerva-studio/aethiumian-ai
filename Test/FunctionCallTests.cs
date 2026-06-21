using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Amlos.AI.Tests
{
    public sealed class FunctionCallTests
    {
        [AIFunction("Tests", "Registered Add")]
        public static int RegisteredAdd(int a, int b) => a + b;

        public static Task<bool> ReturnTask() => Task.FromResult(true);

        public static Task<int> ReturnTaskInt(int value) => Task.FromResult(value);

        public static Task<int> ReturnTaskWithCancellation(CancellationToken token, int value) => Task.FromResult(token.CanBeCanceled ? value : -1);

        public static IEnumerator ReturnEnumerator()
        {
            yield break;
        }

        public static void CompleteWithProgress(NodeProgress progress)
        {
            progress.End(true);
        }

        public int InstanceAdd(int a, int b) => a + b;

        [AIFunction(displayName: "Registered Instance")]
        public int RegisteredInstanceAdd(int a, int b) => a + b;

        public static int UnregisteredStatic(int value) => value;

        public static void CancellationWithoutTask(CancellationToken token)
        {
        }

        public sealed class AlternateReceiver
        {
            public int AlternateOnly(int value) => value;
        }

        [SetUp]
        public void SetUp()
        {
            FunctionRegistry.ClearCache();
        }

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
        public void FunctionRegistry_ReusesCustomFunctionCandidateCache()
        {
            var first = FunctionRegistry.GetCustomFunctions();
            var second = FunctionRegistry.GetCustomFunctions();

            Assert.AreSame(first, second);
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
        public void FunctionRegistry_ReportsActionCapableMethods()
        {
            Assert.True(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(ReturnTask))));
            Assert.True(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(ReturnTaskInt))));
            Assert.True(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(ReturnTaskWithCancellation))));
            Assert.True(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(ReturnEnumerator))));
            Assert.True(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(CompleteWithProgress))));
            Assert.False(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(UnregisteredStatic))));
            Assert.False(FunctionRegistry.IsValidActionMethod(typeof(FunctionCallTests).GetMethod(nameof(CancellationWithoutTask))));
        }

        [Test]
        public void FunctionRegistry_ActionCandidatesOnlyIncludeActionCapableMethods()
        {
            var candidates = FunctionRegistry.GetActionMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Static,
                    "Target Script",
                    FunctionRegistry.ReceiverAssignment.TargetScript,
                    includeUnregisteredFolder: true)
                .ToList();

            Assert.True(candidates.Any(candidate => candidate.Method.Name == nameof(ReturnTask)));
            Assert.True(candidates.Any(candidate => candidate.Method.Name == nameof(ReturnTaskInt)));
            Assert.True(candidates.Any(candidate => candidate.Method.Name == nameof(ReturnEnumerator)));
            Assert.True(candidates.Any(candidate => candidate.Method.Name == nameof(CompleteWithProgress)));
            Assert.False(candidates.Any(candidate => candidate.Method.Name == nameof(UnregisteredStatic)));
        }

        [Test]
        public void FunctionRegistry_ProvidesArithmeticCandidates()
        {
            Assert.True(FunctionRegistry.GetArithmeticFunctions().Any(candidate => candidate.Method.Name == nameof(ArithmeticFunctions.Add)));
        }

        [Test]
        public void FunctionRegistry_CategorizesMathfCandidatesByFunction()
        {
            FunctionRegistry.FunctionCandidate abs = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Abs));
            FunctionRegistry.FunctionCandidate sqrt = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Sqrt));
            FunctionRegistry.FunctionCandidate clamp = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Clamp));
            FunctionRegistry.FunctionCandidate lerp = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Lerp));
            FunctionRegistry.FunctionCandidate sin = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Sin));
            FunctionRegistry.FunctionCandidate deltaAngle = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.DeltaAngle));
            FunctionRegistry.FunctionCandidate perlinNoise = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.PerlinNoise));

            Assert.NotNull(abs);
            Assert.AreEqual("Arithmetic/Number", abs.Path);
            Assert.NotNull(sqrt);
            Assert.AreEqual("Arithmetic/Number", sqrt.Path);
            Assert.NotNull(clamp);
            Assert.AreEqual("Arithmetic/Range", clamp.Path);
            Assert.NotNull(lerp);
            Assert.AreEqual("Arithmetic/Interpolation", lerp.Path);
            Assert.NotNull(sin);
            Assert.AreEqual("Arithmetic/Angles & Trig", sin.Path);
            Assert.NotNull(deltaAngle);
            Assert.AreEqual("Arithmetic/Angles & Trig", deltaAngle.Path);
            Assert.NotNull(perlinNoise);
            Assert.AreEqual("Arithmetic/Waves", perlinNoise.Path);
        }

        [Test]
        public void FunctionRegistry_ArithmeticCandidatesHideDeclaringTypeInDisplay()
        {
            FunctionRegistry.FunctionCandidate abs = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Abs));

            Assert.NotNull(abs);
            Assert.AreEqual("Abs", abs.SortKey);
            Assert.False(abs.DisplaySignature.Contains("Mathf."));
            Assert.True(abs.SearchText.Contains(typeof(Mathf).FullName));
        }

        [Test]
        public void FunctionCandidate_ArithmeticCallableNameHidesDeclaringType()
        {
            FunctionRegistry.FunctionCandidate abs = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(Mathf) && candidate.Method.Name == nameof(Mathf.Abs));

            Assert.NotNull(abs);
            Assert.AreEqual("Abs", abs.GetDisplayCallableName());
            Assert.False(abs.GetDisplayParameterSignature().Contains(nameof(Mathf.Abs)));
            Assert.AreEqual(abs.DisplaySignature, abs.GetFullDisplayName());
        }

        [Test]
        public void FunctionCandidate_ContextCallableNameKeepsDeclaringType()
        {
            FunctionRegistry.FunctionCandidate find = FunctionRegistry
                .GetMethods(
                    typeof(GameObject),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(GameObject.Find));

            Assert.NotNull(find);
            Assert.AreEqual("GameObject.Find", find.GetDisplayCallableName());
            Assert.False(find.GetDisplayParameterSignature().Contains(nameof(GameObject.Find)));
            Assert.AreEqual(find.DisplaySignature, find.GetFullDisplayName());
        }

        [Test]
        public void FunctionCandidate_CustomDisplayNamePrefixesCallableName()
        {
            FunctionRegistry.FunctionCandidate registeredAdd = FunctionRegistry.GetCustomFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(RegisteredAdd));

            Assert.NotNull(registeredAdd);
            Assert.AreEqual("Registered Add  FunctionCallTests.RegisteredAdd", registeredAdd.GetDisplayCallableName());
            Assert.AreEqual("(a: Int32, b: Int32) -> Int32", registeredAdd.GetDisplayParameterSignature());
            Assert.AreEqual($"Registered Add  {registeredAdd.DisplaySignature}", registeredAdd.GetFullDisplayName());
        }

        [Test]
        public void FunctionRegistry_ProvidesMathfConstants()
        {
            FunctionRegistry.FunctionCandidate pi = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.DeclaringType == typeof(ArithmeticFunctions) && candidate.Method.Name == nameof(ArithmeticFunctions.PI));

            Assert.NotNull(pi);
            Assert.AreEqual("Arithmetic/Constants", pi.Path);
            Assert.AreEqual(0, pi.Method.GetParameters().Length);
            Assert.AreEqual(typeof(float), pi.Method.ReturnType);
        }

        [Test]
        public void FunctionRegistry_ArithmeticBuiltinsUseSubfolders()
        {
            FunctionRegistry.FunctionCandidate sineWave = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(ArithmeticFunctions.SineWave));
            FunctionRegistry.FunctionCandidate easeInOutQuad = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(ArithmeticFunctions.EaseInOutQuad));
            FunctionRegistry.FunctionCandidate remap = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(ArithmeticFunctions.Remap));
            FunctionRegistry.FunctionCandidate add = FunctionRegistry.GetArithmeticFunctions()
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(ArithmeticFunctions.Add));

            Assert.NotNull(sineWave);
            Assert.AreEqual("Arithmetic/Waves", sineWave.Path);
            Assert.NotNull(easeInOutQuad);
            Assert.AreEqual("Arithmetic/Interpolation", easeInOutQuad.Path);
            Assert.NotNull(remap);
            Assert.AreEqual("Arithmetic/Range", remap.Path);
            Assert.NotNull(add);
            Assert.AreEqual("Arithmetic/Number", add.Path);
        }

        [Test]
        public void ArithmeticFunctions_SamplingFunctionsEvaluateExpectedValues()
        {
            Assert.AreEqual(0.25f, ArithmeticFunctions.Phase01(2.5f, 2f), 0.0001f);
            Assert.AreEqual(0f, ArithmeticFunctions.Phase01(1f, 0f));
            Assert.AreEqual(5f, ArithmeticFunctions.SineWave(Mathf.PI * 0.5f, 2f, 1f, 0f, 3f), 0.0001f);
            Assert.AreEqual(5f, ArithmeticFunctions.CosineWave(0f, 2f, 1f, 0f, 3f), 0.0001f);
            Assert.AreEqual(1f, ArithmeticFunctions.SineWave01(0.5f, 2f), 0.0001f);
            Assert.AreEqual(1f, ArithmeticFunctions.CosineWave01(0f, 2f), 0.0001f);
            Assert.AreEqual(1f, ArithmeticFunctions.TriangleWave01(1f, 2f), 0.0001f);
            Assert.True(ArithmeticFunctions.Pulse(0.4f, 1f, 0.5f));
            Assert.False(ArithmeticFunctions.Pulse(0.6f, 1f, 0.5f));
            Assert.False(ArithmeticFunctions.Pulse(0f, 0f, 1f));
        }

        [Test]
        public void ArithmeticFunctions_EasingFunctionsClampInput()
        {
            Assert.AreEqual(0f, ArithmeticFunctions.EaseInQuad(-1f));
            Assert.AreEqual(0.5f, ArithmeticFunctions.EaseInOutQuad(0.5f), 0.0001f);
            Assert.AreEqual(1f, ArithmeticFunctions.EaseOutSine(2f), 0.0001f);
        }

        [Test]
        public void ArithmeticFunctions_MappingFunctionsHandleZeroRanges()
        {
            Assert.AreEqual(1f, ArithmeticFunctions.Saturate(2f));
            Assert.AreEqual(50f, ArithmeticFunctions.Remap(5f, 0f, 10f, 0f, 100f), 0.0001f);
            Assert.AreEqual(0.5f, ArithmeticFunctions.Remap01(5f, 0f, 10f), 0.0001f);
            Assert.AreEqual(0f, ArithmeticFunctions.InverseLerpUnclamped(1f, 1f, 5f));
            Assert.AreEqual(10f, ArithmeticFunctions.Remap(5f, 1f, 1f, 10f, 20f));
        }

        [Test]
        public void FunctionRegistry_ResolvesMathfOverloadByMethodIdentity()
        {
            MethodInfo method = typeof(Mathf).GetMethod(nameof(Mathf.Clamp), new[] { typeof(float), typeof(float), typeof(float) });
            FunctionReference reference = new();

            reference.SetMethod(method);

            Assert.AreEqual(method, FunctionRegistry.Resolve(reference));
        }

        [Test]
        public void FunctionRegistry_ArithmeticCandidatesDoNotRepeatMethodSignatures()
        {
            bool hasDuplicate = FunctionRegistry.GetArithmeticFunctions()
                .GroupBy(candidate => $"{candidate.Method.Name}({string.Join("|", candidate.Method.GetParameters().Select(parameter => parameter.ParameterType.FullName))})")
                .Any(group => group.Count() > 1);

            Assert.False(hasDuplicate);
        }

        [Test]
        public void FunctionRegistry_ArithmeticCandidatesUseNoReceiverAssignment()
        {
            Assert.True(FunctionRegistry.GetArithmeticFunctions().All(candidate => candidate.ReceiverAssignment == FunctionRegistry.ReceiverAssignment.None));
        }

        [Test]
        public void FunctionRegistry_ArithmeticCandidatesSkipUnsupportedParameterShapes()
        {
            Assert.False(FunctionRegistry.GetArithmeticFunctions()
                .SelectMany(candidate => candidate.Method.GetParameters())
                .Any(parameter => parameter.ParameterType.IsByRef || parameter.ParameterType.IsArray || parameter.ParameterType.IsPointer));
        }

        [Test]
        public void FunctionRegistry_ReusesArithmeticCandidateCache()
        {
            var first = FunctionRegistry.GetArithmeticFunctions();
            var second = FunctionRegistry.GetArithmeticFunctions();

            Assert.AreSame(first, second);
        }

        [Test]
        public void FunctionRegistry_ClearCacheRebuildsArithmeticCandidates()
        {
            var first = FunctionRegistry.GetArithmeticFunctions();

            FunctionRegistry.ClearCache();

            var second = FunctionRegistry.GetArithmeticFunctions();

            Assert.AreNotSame(first, second);
        }

        [Test]
        public void FunctionRegistry_ClearCacheIncrementsCacheVersion()
        {
            int firstVersion = FunctionRegistry.CacheVersion;

            FunctionRegistry.ClearCache();

            Assert.Greater(FunctionRegistry.CacheVersion, firstVersion);
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
        public void FunctionRegistry_ReusesObjectCandidateCacheByReceiverType()
        {
            var first = FunctionRegistry.GetMethods(
                typeof(FunctionCallTests),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                "Object",
                FunctionRegistry.ReceiverAssignment.Preserve,
                includeUnregisteredFolder: true);
            var second = FunctionRegistry.GetMethods(
                typeof(FunctionCallTests),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                "Object",
                FunctionRegistry.ReceiverAssignment.Preserve,
                includeUnregisteredFolder: true);

            Assert.AreSame(first, second);
        }

        [Test]
        public void FunctionRegistry_ObjectCandidateCacheSeparatesReceiverTypes()
        {
            var primary = FunctionRegistry.GetMethods(
                typeof(FunctionCallTests),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                "Object",
                FunctionRegistry.ReceiverAssignment.Preserve,
                includeUnregisteredFolder: true);
            var alternate = FunctionRegistry.GetMethods(
                typeof(AlternateReceiver),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                "Object",
                FunctionRegistry.ReceiverAssignment.Preserve,
                includeUnregisteredFolder: true);

            Assert.True(primary.Any(candidate => candidate.Method.Name == nameof(InstanceAdd)));
            Assert.False(primary.Any(candidate => candidate.Method.Name == nameof(AlternateReceiver.AlternateOnly)));
            Assert.True(alternate.Any(candidate => candidate.Method.Name == nameof(AlternateReceiver.AlternateOnly)));
            Assert.False(alternate.Any(candidate => candidate.Method.Name == nameof(InstanceAdd)));
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
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(GameObject.CompareTag));

            FunctionRegistry.FunctionCandidate transformCandidate = FunctionRegistry
                .GetMethods(
                    typeof(Transform),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
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
        public void FunctionRegistry_GameObjectStaticMethodsStayAtContextRootWithoutReceiver()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(GameObject),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(GameObject.Find));

            Assert.NotNull(candidate);
            Assert.AreEqual("GameObject", candidate.Path);
            Assert.False(candidate.RequiresReceiver);
            Assert.IsNull(candidate.GetDisplayReceiverType());
            Assert.AreEqual(FunctionRegistry.ReceiverAssignment.None, candidate.ReceiverAssignment);
        }

        [Test]
        public void FunctionRegistry_GameObjectStaticMethodsShowDeclaringTypeInDisplay()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(GameObject),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    "GameObject",
                    FunctionRegistry.ReceiverAssignment.GameObject,
                    includeUnregisteredFolder: false)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(GameObject.Find));

            Assert.NotNull(candidate);
            Assert.AreEqual("GameObject.Find", candidate.SortKey);
            Assert.True(candidate.DisplaySignature.StartsWith("GameObject.Find"));
        }

        [Test]
        public void FunctionRegistry_ReusesContextMethodCandidateCache()
        {
            var first = FunctionRegistry.GetMethods(
                typeof(GameObject),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                "GameObject",
                FunctionRegistry.ReceiverAssignment.GameObject,
                includeUnregisteredFolder: false);
            var second = FunctionRegistry.GetMethods(
                typeof(GameObject),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                "GameObject",
                FunctionRegistry.ReceiverAssignment.GameObject,
                includeUnregisteredFolder: false);

            Assert.AreSame(first, second);
        }

        [Test]
        public void FunctionRegistry_ContextStaticMethodsUseNoReceiverAssignment()
        {
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                    "Target Script",
                    FunctionRegistry.ReceiverAssignment.TargetScript,
                    includeUnregisteredFolder: true)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(UnregisteredStatic));

            Assert.NotNull(candidate);
            Assert.AreEqual("Target Script/Unregistered", candidate.Path);
            Assert.False(candidate.RequiresReceiver);
            Assert.AreEqual(FunctionRegistry.ReceiverAssignment.None, candidate.ReceiverAssignment);
        }

        [Test]
        public void FunctionRegistry_ObjectStaticMethodsClearReceiverOnSelection()
        {
            VariableData manualReceiver = new("Manual Receiver", VariableType.UnityObject);
            FunctionReference reference = new();
            FunctionRegistry.FunctionCandidate candidate = FunctionRegistry
                .GetMethods(
                    typeof(FunctionCallTests),
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance,
                    "Object",
                    FunctionRegistry.ReceiverAssignment.Preserve,
                    includeUnregisteredFolder: true)
                .FirstOrDefault(candidate => candidate.Method.Name == nameof(UnregisteredStatic));

            Assert.NotNull(candidate);

            reference.targetObject.SetReference(manualReceiver);
            reference.SetMethod(candidate.Method);
            FunctionRegistry.AssignReceiverResource(reference, candidate.ReceiverAssignment);

            Assert.False(reference.targetObject.HasEditorReference);
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
            reference.SetMethod(typeof(ArithmeticFunctions).GetMethod(nameof(ArithmeticFunctions.Add)));
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
