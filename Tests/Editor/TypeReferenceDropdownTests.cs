using Aethiumian.AI.Editor;
using Aethiumian.AI.References;
using NUnit.Framework;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests
{
    /// <summary>Visible base type used to exercise the production type catalogue.</summary>
    public class TypeReferenceDropdownTestBase { }

    /// <summary>Concrete visible derived type expected in the dropdown.</summary>
    public sealed class TypeReferenceDropdownTestConcrete : TypeReferenceDropdownTestBase { }

    /// <summary>Abstract derived type that must not appear in the dropdown.</summary>
    public abstract class TypeReferenceDropdownTestAbstract : TypeReferenceDropdownTestBase { }

    /// <summary>Open generic derived type that must not appear in the dropdown.</summary>
    public class TypeReferenceDropdownTestOpenGeneric<T> : TypeReferenceDropdownTestBase { }

    /// <summary>Serialized host used to verify the production property write path.</summary>
    public sealed class TypeReferenceDropdownTestAsset : ScriptableObject
    {
        public TypeReference<TypeReferenceDropdownTestBase> reference = new();
    }

    /// <summary>Validates the production type catalogue and serialized selection path.</summary>
    public sealed class TypeReferenceDropdownTests
    {
        private TypeReferenceDropdownTestAsset asset;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();
            asset = ScriptableObject.CreateInstance<TypeReferenceDropdownTestAsset>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (asset)
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Catalogue_IncludesConcreteBaseAndDerived_ExcludesAbstractAndOpenGeneric()
        {
            var candidates = TypeReferenceDropdownCatalogue.GetCandidates(typeof(TypeReferenceDropdownTestBase));

            Assert.That(candidates.Contains(typeof(TypeReferenceDropdownTestBase)), Is.True);
            Assert.That(candidates.Contains(typeof(TypeReferenceDropdownTestConcrete)), Is.True);
            Assert.That(candidates.Contains(typeof(TypeReferenceDropdownTestAbstract)), Is.False);
            Assert.That(candidates.Contains(typeof(TypeReferenceDropdownTestOpenGeneric<>)), Is.False);
            Assert.That(candidates.All(type =>
                type.IsVisible
                && !type.IsGenericTypeDefinition
                && typeof(TypeReferenceDropdownTestBase).IsAssignableFrom(type)), Is.True);
        }

        [Test]
        public void Catalogue_IsStableAndCaseInsensitiveSorted()
        {
            var candidates = TypeReferenceDropdownCatalogue.GetCandidates(typeof(TypeReferenceDropdownTestBase));
            var expected = candidates
                .OrderBy(type => type.Namespace ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.That(candidates, Is.EqualTo(expected));
            Assert.That(candidates.Distinct().Count(), Is.EqualTo(candidates.Count));
        }

        [Test]
        public void TryApplySelection_ConcreteTypeWritesSerializedFullNameAndAssembly()
        {
            Assert.That(TypeReferencePropertyDrawer.TryApplySelection(
                asset,
                nameof(TypeReferenceDropdownTestAsset.reference),
                typeof(TypeReferenceDropdownTestConcrete)), Is.True);

            SerializedObject serializedObject = new(asset);
            serializedObject.Update();
            TypeReference<TypeReferenceDropdownTestBase> reference =
                serializedObject.FindProperty(nameof(TypeReferenceDropdownTestAsset.reference)).boxedValue
                as TypeReference<TypeReferenceDropdownTestBase>;

            Assert.That(reference.fullName, Is.EqualTo(typeof(TypeReferenceDropdownTestConcrete).FullName));
            Assert.That(reference.assemblyName, Is.EqualTo(typeof(TypeReferenceDropdownTestConcrete).Assembly.GetName().Name));
        }

        [Test]
        public void TryApplySelection_NoneClearsValue_AndUndoRestoresIt()
        {
            asset.reference.SetReferType(typeof(TypeReferenceDropdownTestConcrete));
            SerializedObject seed = new(asset);
            seed.Update();
            seed.FindProperty(nameof(TypeReferenceDropdownTestAsset.reference)).boxedValue = asset.reference;
            seed.ApplyModifiedProperties();

            Assert.That(TypeReferencePropertyDrawer.TryApplySelection(
                asset,
                nameof(TypeReferenceDropdownTestAsset.reference),
                null), Is.True);

            SerializedObject serializedObject = new(asset);
            serializedObject.Update();
            TypeReference<TypeReferenceDropdownTestBase> cleared =
                serializedObject.FindProperty(nameof(TypeReferenceDropdownTestAsset.reference)).boxedValue
                as TypeReference<TypeReferenceDropdownTestBase>;
            Assert.That(cleared.fullName, Is.Empty);
            Assert.That(cleared.assemblyName, Is.Empty);

            Undo.PerformUndo();
            serializedObject.Update();
            TypeReference<TypeReferenceDropdownTestBase> restored =
                serializedObject.FindProperty(nameof(TypeReferenceDropdownTestAsset.reference)).boxedValue
                as TypeReference<TypeReferenceDropdownTestBase>;
            Assert.That(restored.fullName, Is.EqualTo(typeof(TypeReferenceDropdownTestConcrete).FullName));
        }

        [Test]
        public void TryApplySelection_StalePropertyPathReturnsFalseAndLeavesValueUnchanged()
        {
            string before = asset.reference.fullName;
            Assert.That(TypeReferencePropertyDrawer.TryApplySelection(
                asset,
                "missingReference",
                typeof(TypeReferenceDropdownTestConcrete)), Is.False);

            Assert.That(asset.reference.fullName, Is.EqualTo(before));
            Assert.That(asset.reference.assemblyName, Is.Empty);
        }
    }
}
