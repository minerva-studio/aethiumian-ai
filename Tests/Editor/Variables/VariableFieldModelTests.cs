using Aethiumian.AI.Variables;
using Aethiumian.AI.Editor;
using NUnit.Framework;
using System;
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
        public void GenericField_DoesNotUseDynamicTypeSemantics()
        {
            VariableField<int> field = 7;

            Assert.That(field.Type, Is.EqualTo(VariableType.Int));
            Assert.That(field.FieldObjectType, Is.EqualTo(typeof(int)));
            Assert.That(field.IsDynamicType, Is.False);
            Assert.That(field.Constant, Is.EqualTo(7));
        }

        [Test]
        public void DynamicField_UsesPayloadAndDynamicTypeSemantics()
        {
            VariableField field = new(VariableType.Float);
            field.ForceSetConstantValue(2.5f);

            Assert.That(field.Type, Is.EqualTo(VariableType.Float));
            Assert.That(field.FieldObjectType, Is.EqualTo(typeof(object)));
            Assert.That(field.IsDynamicType, Is.True);
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
            Assert.That(parameter.IsDynamicType, Is.True);
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

            Assert.That(fixedReference.IsDynamicType, Is.False);
            Assert.That(fixedReference.FieldObjectType, Is.EqualTo(typeof(int)));
            Assert.That(dynamicReference.IsDynamicType, Is.True);
            Assert.That(dynamicReference.FieldObjectType, Is.EqualTo(typeof(object)));
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
    }
}
