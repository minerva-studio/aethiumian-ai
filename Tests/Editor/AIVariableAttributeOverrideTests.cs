using Aethiumian.AI.Variables;
using NUnit.Framework;
using System.Linq;

namespace Aethiumian.AI.Tests
{
    public sealed class AIVariableAttributeOverrideTests
    {
        [Test]
        public void OverridePropertyWithAttributeUsesDerivedVariableOnly()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedPropertyWithAttributeScript));

            Assert.That(variables.Count(v => v.name == "Derived Override Property"), Is.EqualTo(1));
            Assert.That(variables.Any(v => v.name == "Base Override Property"), Is.False);

            VariableData variable = variables.Single(v => v.name == "Derived Override Property");
            Assert.That(variable.Path, Is.EqualTo(nameof(DerivedPropertyWithAttributeScript.Value)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));
        }

        [Test]
        public void OverridePropertyWithoutAttributeInheritsBaseVariableAndUsesDerivedGetter()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedPropertyWithoutAttributeScript));

            VariableData variable = variables.Single(v => v.name == "Inherited Base Property");
            Assert.That(variable.Path, Is.EqualTo(nameof(DerivedPropertyWithoutAttributeScript.Value)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));

            var runtimeVariable = new TargetScriptVariable(variable, new DerivedPropertyWithoutAttributeScript());
            Assert.That(runtimeVariable.GetValue<int>(), Is.EqualTo(2));
        }

        [Test]
        public void OverrideMethodWithAttributeUsesDerivedVariableOnly()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedMethodWithAttributeScript));

            Assert.That(variables.Count(v => v.name == "Derived Override Method"), Is.EqualTo(1));
            Assert.That(variables.Any(v => v.name == "Base Override Method"), Is.False);

            VariableData variable = variables.Single(v => v.name == "Derived Override Method");
            Assert.That(variable.Path, Is.EqualTo(nameof(DerivedMethodWithAttributeScript.GetValue)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));
        }

        [Test]
        public void OverrideMethodWithoutAttributeInheritsBaseVariableAndUsesDerivedMethod()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedMethodWithoutAttributeScript));

            VariableData variable = variables.Single(v => v.name == "Inherited Base Method");
            Assert.That(variable.Path, Is.EqualTo(nameof(DerivedMethodWithoutAttributeScript.GetValue)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));

            var runtimeVariable = new TargetScriptVariable(variable, new DerivedMethodWithoutAttributeScript());
            Assert.That(runtimeVariable.GetValue<int>(), Is.EqualTo(2));
        }

        [Test]
        public void CharacterTargetStyleOverrideWithoutAttributeIsValid()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(EnemyStyleScript));

            VariableData variable = variables.Single(v => v.name == nameof(CharacterStyleScript.Target));
            Assert.That(variable.Path, Is.EqualTo(nameof(EnemyStyleScript.Target)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));

            var runtimeVariable = new TargetScriptVariable(variable, new EnemyStyleScript());
            Assert.That(runtimeVariable.GetValue<int>(), Is.EqualTo(5));
        }

        [Test]
        public void HiddenFieldWithAttributeUsesDerivedVariableOnly()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedHiddenFieldScript));

            Assert.That(variables.Count(v => v.name == "Derived Hidden Field"), Is.EqualTo(1));
            Assert.That(variables.Any(v => v.name == "Base Hidden Field"), Is.False);

            VariableData variable = variables.Single(v => v.name == "Derived Hidden Field");
            Assert.That(variable.Path, Is.EqualTo(nameof(DerivedHiddenFieldScript.value)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));
        }

        [Test]
        public void BaseAttributeMemberWithoutOverrideIsStillCollected()
        {
            var variables = AIVariableAttribute.GetAttributeVariablesFromType(typeof(DerivedBaseOnlyPropertyScript));

            VariableData variable = variables.Single(v => v.name == "Base Only Property");
            Assert.That(variable.Path, Is.EqualTo(nameof(BaseOnlyPropertyScript.BaseOnly)));
            Assert.That(variable.Type, Is.EqualTo(VariableType.Int));
        }

        private class BasePropertyWithAttributeScript
        {
            [AIVariable("Base Override Property")]
            public virtual int Value => 1;
        }

        private sealed class DerivedPropertyWithAttributeScript : BasePropertyWithAttributeScript
        {
            [AIVariable("Derived Override Property")]
            public override int Value => 2;
        }

        private class BasePropertyWithoutDerivedAttributeScript
        {
            [AIVariable("Inherited Base Property")]
            public virtual int Value => 1;
        }

        private sealed class DerivedPropertyWithoutAttributeScript : BasePropertyWithoutDerivedAttributeScript
        {
            public override int Value => 2;
        }

        private class BaseMethodWithAttributeScript
        {
            [AIVariable("Base Override Method")]
            public virtual int GetValue()
            {
                return 1;
            }
        }

        private sealed class DerivedMethodWithAttributeScript : BaseMethodWithAttributeScript
        {
            [AIVariable("Derived Override Method")]
            public override int GetValue()
            {
                return 2;
            }
        }

        private class BaseMethodWithoutDerivedAttributeScript
        {
            [AIVariable("Inherited Base Method")]
            public virtual int GetValue()
            {
                return 1;
            }
        }

        private sealed class DerivedMethodWithoutAttributeScript : BaseMethodWithoutDerivedAttributeScript
        {
            public override int GetValue()
            {
                return 2;
            }
        }

        private class CharacterStyleScript
        {
            [AIVariable(nameof(Target))]
            public virtual int Target => 3;
        }

        private sealed class EnemyStyleScript : CharacterStyleScript
        {
            public override int Target => 5;
        }

        private class BaseHiddenFieldScript
        {
            [AIVariable("Base Hidden Field")]
            public int value;
        }

        private sealed class DerivedHiddenFieldScript : BaseHiddenFieldScript
        {
            [AIVariable("Derived Hidden Field")]
            public new int value;
        }

        private class BaseOnlyPropertyScript
        {
            [AIVariable("Base Only Property")]
            public int BaseOnly => 1;
        }

        private sealed class DerivedBaseOnlyPropertyScript : BaseOnlyPropertyScript
        {
        }
    }
}
