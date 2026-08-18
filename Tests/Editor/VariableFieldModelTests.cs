using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests
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
        public void VariableReferences_AreSiblingFixedAndDynamicTypes()
        {
            VariableReference<int> fixedReference = new();
            VariableReference dynamicReference = new();

            Assert.That(fixedReference.IsDynamicType, Is.False);
            Assert.That(fixedReference.FieldObjectType, Is.EqualTo(typeof(int)));
            Assert.That(dynamicReference.IsDynamicType, Is.True);
            Assert.That(dynamicReference.FieldObjectType, Is.EqualTo(typeof(object)));
        }
    }
}
