using Aethiumian.AI.Variables;
using Aethiumian.AI.Editor;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    public sealed class VariableFieldModelTests
    {
        [Test]
        public void GenericLayerMaskField_ConvertsToIntegerValue()
        {
            VariableField<LayerMask> field = (LayerMask)1088;

            Assert.That(field.Constant.value, Is.EqualTo(1088));
            Assert.That(field.IntValue, Is.EqualTo(1088));
        }

        [Test]
        public void GenericField_DoesNotImplementDynamicTypeContract()
        {
            VariableField<int> field = 7;

            Assert.That(field.Type, Is.EqualTo(VariableType.Int));
            Assert.That(field.FieldObjectType, Is.EqualTo(typeof(int)));
            Assert.That(field, Is.Not.InstanceOf<IDynamicVariableField>());
            Assert.That(field.Constant, Is.EqualTo(7));
        }

        [Test]
        public void DynamicField_UsesPayloadAndDynamicTypeContract()
        {
            VariableField field = new(VariableType.Float);
            field.ForceSetConstantValue(2.5f);

            Assert.That(field.Type, Is.EqualTo(VariableType.Float));
            Assert.That(field.FieldObjectType, Is.EqualTo(typeof(object)));
            Assert.That(field, Is.InstanceOf<IDynamicVariableField>());
            Assert.That(field.FloatValue, Is.EqualTo(2.5f));
        }

        [Test]
        public void DynamicIntegerConstants_ConvertWhenReadAsFloats()
        {
            VariableField min = new(VariableType.Int);
            VariableField max = new(VariableType.Int);
            min.ForceSetConstantValue(2);
            max.ForceSetConstantValue(10);

            Assert.That(min.Type, Is.EqualTo(VariableType.Int));
            Assert.That(max.Type, Is.EqualTo(VariableType.Int));
            Assert.That(min.FloatValue, Is.EqualTo(2f));
            Assert.That(max.FloatValue, Is.EqualTo(10f));
        }

        [Test]
        public void Parameter_IsDynamicFieldWithoutBeingVariableField()
        {
            Parameter parameter = new(VariableType.Int);

            Assert.That(parameter, Is.InstanceOf<DynamicVariableFieldBase>());
            Assert.That(parameter, Is.Not.InstanceOf<VariableField>());
            Assert.That(parameter, Is.InstanceOf<IDynamicVariableField>());
        }

        [Test]
        public void IntegerParameterOverride_ResolvesEnumTypeWithoutSerialization()
        {
            Parameter parameter = new(VariableType.Int);

            Assert.That(VariableFieldDrawers.ResolveIntegerObjectType(parameter, typeof(ParameterEnum)), Is.EqualTo(typeof(ParameterEnum)));
        }

        [Test]
        public void IntegerParameterOverride_ResolvesFlagsEnumType()
        {
            Parameter parameter = new(VariableType.Int);

            Type resolved = VariableFieldDrawers.ResolveIntegerObjectType(parameter, typeof(ParameterFlags));

            Assert.That(resolved, Is.EqualTo(typeof(ParameterFlags)));
            Assert.That(Attribute.IsDefined(resolved, typeof(FlagsAttribute)), Is.True);
        }

        [Test]
        public void IntegerParameterOverride_ResolvesLayerMaskType()
        {
            Parameter parameter = new(VariableType.Int);

            Assert.That(VariableFieldDrawers.ResolveIntegerObjectType(parameter, typeof(LayerMask)), Is.EqualTo(typeof(LayerMask)));
        }

        [Test]
        public void IntegerParameterWithoutOverride_UsesExistingObjectType()
        {
            Parameter parameter = new(VariableType.Int);

            Assert.That(VariableFieldDrawers.ResolveIntegerObjectType(parameter, null), Is.Null);
        }

        [Test]
        public void VariableReferences_AreSiblingFixedAndDynamicTypes()
        {
            VariableReference<int> fixedReference = new();
            VariableReference dynamicReference = new();

            Assert.That(fixedReference, Is.Not.InstanceOf<IDynamicVariableField>());
            Assert.That(fixedReference.FieldObjectType, Is.EqualTo(typeof(int)));
            Assert.That(dynamicReference, Is.InstanceOf<IDynamicVariableField>());
            Assert.That(dynamicReference.FieldObjectType, Is.EqualTo(typeof(object)));
        }

        [Test]
        public void DynamicVariableTypeContract_IsMarkerOnly()
        {
            Assert.That(typeof(IDynamicVariableField).GetMembers(), Is.Empty);
        }

        [Test]
        public void VariableFieldEditorMetadata_PreservesFixedAndDynamicTypeRules()
        {
            FieldInfo constrainedMember = GetMetadataMember(nameof(MetadataHost.constrained));
            FieldInfo defaultMember = GetMetadataMember(nameof(MetadataHost.dynamicDefault));
            FieldInfo fixedMember = GetMetadataMember(nameof(MetadataHost.fixedValue));

            IReadOnlyList<VariableType> constrained = VariableFieldEditorMetadata.GetAllowedTypes(new VariableField(VariableType.Int), constrainedMember);
            IReadOnlyList<VariableType> constrainedAgain = VariableFieldEditorMetadata.GetAllowedTypes(new VariableField(VariableType.Int), constrainedMember);
            IReadOnlyList<VariableType> defaultTypes = VariableFieldEditorMetadata.GetAllowedTypes(new VariableField(VariableType.Int), defaultMember);
            IReadOnlyList<VariableType> fixedTypes = VariableFieldEditorMetadata.GetAllowedTypes(new VariableField<int>(), fixedMember);

            Assert.That(constrained, Is.EqualTo(new[] { VariableType.Vector3, VariableType.Vector2 }));
            Assert.That(defaultTypes.Count, Is.EqualTo(Enum.GetValues(typeof(VariableType)).Length));
            Assert.That(fixedTypes, Is.EqualTo(new[] { VariableType.Int }));
            Assert.That(ReferenceEquals(constrained, constrainedAgain), Is.True);
            Assert.That(ReferenceEquals(defaultTypes, VariableTypeCatalog.GetAllVariableTypes()), Is.True);
            Assert.That(ReferenceEquals(fixedTypes, VariableTypeCatalog.GetSingleType(VariableType.Int)), Is.True);
        }

        [Test]
        public void VariableFieldEditorMetadata_CombinesAccessFlagsAndCachesWarmLookups()
        {
            FieldInfo member = GetMetadataMember(nameof(MetadataHost.constrained));
            VariableField dynamicField = new(VariableType.Int);
            VariableAccessFlag expected = VariableAccessFlag.Read | VariableAccessFlag.Write;

            Assert.That(VariableFieldEditorMetadata.GetAccessFlag(member), Is.EqualTo(expected));

            _ = VariableFieldEditorMetadata.GetAllowedTypes(dynamicField, member);
            _ = VariableFieldEditorMetadata.GetAccessFlag(member);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                _ = VariableFieldEditorMetadata.GetAllowedTypes(dynamicField, member);
                _ = VariableFieldEditorMetadata.GetAccessFlag(member);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void VariableFieldBase_OnlyOwnsBindingContract()
        {
            Assert.That(typeof(VariableFieldBase).GetProperty("IsConstant", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(VariableFieldBase).GetProperty("ConstantBoxed", BindingFlags.Instance | BindingFlags.Public), Is.Null);
            Assert.That(typeof(VariableFieldBase).GetMethod("ForceSetConstantValue", BindingFlags.Instance | BindingFlags.Public), Is.Null);
        }

        [Test]
        public void ValueField_ConstantAndBindingStatesAreDistinct()
        {
            VariableField<int> field = 7;
            VariableData authoredReference = new("Runtime value", VariableType.Int);
            TreeVariable runtimeVariable = CreateTreeVariable(VariableType.Int, 11);

            Assert.That(field.IsConstant, Is.True);
            Assert.That(field.HasValue, Is.True);
            Assert.That(field.GetValue<int>(), Is.EqualTo(7));

            field.SetReference(authoredReference);

            Assert.That(field.IsConstant, Is.False);
            Assert.That(field.HasValue, Is.False);
            Assert.That(() => field.GetValue<int>(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => field.Value, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => field.Constant, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => field.SetValue(9), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => field.ForceSetConstantValue(9), Throws.TypeOf<InvalidOperationException>());

            field.SetRuntimeReference(runtimeVariable);

            Assert.That(field.IsConstant, Is.False);
            Assert.That(field.HasValue, Is.True);
            Assert.That(field.GetValue<int>(), Is.EqualTo(11));
        }

        [Test]
        public void EmptyAndUnresolvedReferencesHaveDifferentReadSemantics()
        {
            VariableReference<int> emptyReference = new();
            VariableReference<int> unresolvedReference = new();
            unresolvedReference.SetReference(new VariableData("Missing", VariableType.Int));

            Assert.That(emptyReference.HasEditorReference, Is.False);
            Assert.That(emptyReference.HasValue, Is.False);
            Assert.That(emptyReference.Value, Is.Null);
            Assert.That(emptyReference.GetValue<int>(), Is.EqualTo(0));
            Assert.That(emptyReference.IsNull, Is.True);

            Assert.That(unresolvedReference.HasEditorReference, Is.True);
            Assert.That(unresolvedReference.HasValue, Is.False);
            Assert.That(() => unresolvedReference.Value, Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => unresolvedReference.GetValue<int>(), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => unresolvedReference.SetValue(3), Throws.TypeOf<InvalidOperationException>());
            Assert.That(() => unresolvedReference.IsNull, Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void SetReferenceNullRestoresConstantOnlyForValueFields()
        {
            VariableField<int> valueField = 7;
            VariableReference<int> referenceField = new();
            VariableData authoredReference = new("Runtime value", VariableType.Int);

            valueField.SetReference(authoredReference);
            referenceField.SetReference(authoredReference);

            valueField.SetReference(null);
            referenceField.SetReference(null);

            Assert.That(valueField.IsConstant, Is.True);
            Assert.That(valueField.HasValue, Is.True);
            Assert.That(valueField.GetValue<int>(), Is.EqualTo(7));
            Assert.That(referenceField.HasEditorReference, Is.False);
            Assert.That(referenceField.HasValue, Is.False);
            Assert.That(referenceField.GetValue<int>(), Is.EqualTo(0));
        }

        [Test]
        public void SetRuntimeReferenceNullPreservesAuthoredReference()
        {
            VariableField<int> valueField = 7;
            VariableData authoredReference = new("Runtime value", VariableType.Int);

            valueField.SetReference(authoredReference);
            valueField.SetRuntimeReference(null);

            Assert.That(valueField.HasEditorReference, Is.True);
            Assert.That(valueField.IsConstant, Is.False);
            Assert.That(valueField.HasValue, Is.False);
            Assert.That(() => valueField.GetValue<int>(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GenericField_CommonConstantConversionsPreserveValues()
        {
            VariableField<int> integer = 7;
            VariableField<float> real = 2.9f;
            VariableField<LayerMask> mask = (LayerMask)1088;
            VariableField<Vector4> vector = new Vector4(1, 2, 3, 4);

            Assert.That(integer.GetValue<int>(), Is.EqualTo(7));
            Assert.That(integer.GetValue<float>(), Is.EqualTo(7f));
            Assert.That(real.GetValue<int>(), Is.EqualTo(2));
            Assert.That(mask.GetValue<int>(), Is.EqualTo(1088));
            Assert.That(vector.GetValue<Vector4>(), Is.EqualTo(vector.Constant));
        }

        [Test]
        public void GenericField_SameTypeReadsDoNotAllocateAfterWarmup()
        {
            VariableField<int> field = 7;
            _ = field.GetValue<int>();

            long before = GC.GetAllocatedBytesForCurrentThread();
            int sink = 0;
            for (int i = 0; i < 1000; i++) sink ^= field.GetValue<int>();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(sink, Is.EqualTo(0));
            Assert.That(allocated, Is.EqualTo(0));
        }

        [Test]
        public void TreeVariable_StrongTypedAccessPreservesConversionSemantics()
        {
            TreeVariable integer = CreateTreeVariable(VariableType.Int, 7);
            TreeVariable real = CreateTreeVariable(VariableType.Float, 2.9f);
            TreeVariable vector = CreateTreeVariable(VariableType.Vector3, new Vector3(1, 2, 3));

            Assert.That(integer.IntValue, Is.EqualTo(7));
            Assert.That(integer.FloatValue, Is.EqualTo(7f));
            Assert.That(real.IntValue, Is.EqualTo(2));
            Assert.That(vector.Vector2Value, Is.EqualTo(new Vector2(1, 2)));
            Assert.That(vector.Vector4Value, Is.EqualTo(new Vector4(1, 2, 3, 0)));
        }

        [Test]
        public void GenericFieldAndReference_ReadRuntimeVariableWithoutBoxingBoundary()
        {
            TreeVariable variable = CreateTreeVariable(VariableType.Int, 7);
            VariableField<int> field = new();
            VariableReference<int> reference = new();
            field.SetRuntimeReference(variable);
            reference.SetRuntimeReference(variable);

            Assert.That(field.IntValue, Is.EqualTo(7));
            Assert.That(reference.IntValue, Is.EqualTo(7));
            Assert.That((int)reference, Is.EqualTo(7));
        }

        [Test]
        public void StrongTypedVariableFamilyReadsDoNotAllocateAfterWarmup()
        {
            TreeVariable variable = CreateTreeVariable(VariableType.Int, 7);
            VariableField<int> field = new();
            VariableReference<int> reference = new();
            VariableField dynamicField = new(VariableType.Int);
            field.SetRuntimeReference(variable);
            reference.SetRuntimeReference(variable);
            dynamicField.ForceSetConstantValue(7);
            _ = variable.IntValue;
            _ = variable.GetValue<int>();
            _ = field.IntValue;
            _ = reference.IntValue;
            _ = dynamicField.IntValue;

            long before = GC.GetAllocatedBytesForCurrentThread();
            int sink = 0;
            for (int i = 0; i < 1000; i++)
            {
                sink ^= variable.IntValue;
                sink ^= variable.GetValue<int>();
                sink ^= field.IntValue;
                sink ^= reference.IntValue;
                sink ^= dynamicField.IntValue;
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(sink, Is.EqualTo(0));
            Assert.That(allocated, Is.EqualTo(0));
        }

        private static TreeVariable CreateTreeVariable<T>(VariableType type, T value)
        {
            VariableData data = new("Runtime value", type);
            TreeVariable variable = new(data);
            variable.SetValue(value);
            return variable;
        }

        private enum ParameterEnum
        {
            A,
            B
        }

        [Flags]
        private enum ParameterFlags
        {
            None = 0,
            A = 1,
            B = 2
        }

        private static FieldInfo GetMetadataMember(string name)
        {
            return typeof(MetadataHost).GetField(name, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new AssertionException($"Metadata member {name} was not found.");
        }

        private sealed class MetadataHost
        {
            [Constraint(VariableType.Vector3, VariableType.Int, VariableType.Vector2)]
            [Exclude(VariableType.Int)]
            [Readable]
            [Writable]
            public VariableField constrained;

            public VariableField dynamicDefault;
            public VariableField<int> fixedValue;
        }
    }
}
