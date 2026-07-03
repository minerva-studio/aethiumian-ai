using Aethiumian.AI.Accessors;
using Aethiumian.AI.Attributes;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using UnityEngine;
using Ator = UnityEngine.Animator;

namespace Aethiumian.AI.Nodes
{
    /// <summary>
    /// New version of animator parameter control
    /// </summary>
    [NodeTip("Change parameters of an animator")]
    [Serializable]
    [RequireComponent(typeof(Ator))]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class Animator : Call
    {
        [Serializable]
        public class Parameter : IDuplicable, IVariableField
        {
            public bool use;

            public string parameter;
            public ParameterType type;

            [DisplayIf(nameof(type), ParameterType.@int)] public VariableField<int> valueInt = new();
            [DisplayIf(nameof(type), ParameterType.@float)] public VariableField<float> valueFloat = new();
            [DisplayIf(nameof(type), ParameterType.@bool)] public VariableField<bool> valueBool = new();
            [DisplayIf(nameof(type), ParameterType.trigger)] public TriggerSet setTrigger;

            private IVariableField ActiveVariableField
            {
                get
                {
                    return type switch
                    {
                        ParameterType.@int => valueInt,
                        ParameterType.@float => valueFloat,
                        ParameterType.@bool => valueBool,
                        _ => null,
                    };
                }
            }

            VariableType IVariableField.Type => ActiveVariableField?.Type ?? VariableType.Invalid;

            UUID IVariableField.UUID => ActiveVariableField?.UUID ?? UUID.Empty;

            bool IVariableField.IsConstant => ActiveVariableField?.IsConstant ?? true;

            Variable IVariableField.Variable => ActiveVariableField?.Variable;

            object IVariableField.Value => ActiveVariableField?.Value;

            void IVariableField.SetReference(VariableData variable)
            {
                ActiveVariableField?.SetReference(variable);
            }

            void IVariableField.SetRuntimeReference(Variable variable)
            {
                ActiveVariableField?.SetRuntimeReference(variable);
            }

            public object Duplicate()
            {
                return new Parameter()
                {
                    use = use,
                    parameter = parameter,
                    type = type,
                    valueInt = global::Aethiumian.AI.Accessors.Duplicate.Value(valueInt),
                    valueFloat = global::Aethiumian.AI.Accessors.Duplicate.Value(valueFloat),
                    valueBool = global::Aethiumian.AI.Accessors.Duplicate.Value(valueBool),
                    setTrigger = setTrigger,
                };
            }
        }


        public enum ParameterType
        {
            invalid = -1,
            @trigger,
            @int,
            @float,
            @bool,
        }

        public enum TriggerSet
        {
            set,
            reset,
        }


        public List<Parameter> parameters = new();



        Ator animator;
        Ator AnimatorComponent => animator ? animator : animator = gameObject.GetComponent<Ator>();



        public override State Execute()
        {
            //AddSelfToProgress();
            if (!AnimatorComponent)
            {
                return State.Failed;
            }
            foreach (var item in parameters)
            {
                if (!item.use) continue;

                var type = item.type;
                var parameter = item.parameter;
                switch (type)
                {
                    case ParameterType.trigger:
                        if (item.setTrigger == TriggerSet.set) AnimatorComponent.SetTrigger(item.parameter);
                        else AnimatorComponent.ResetTrigger(parameter);
                        break;
                    case ParameterType.@int:
                        AnimatorComponent.SetInteger(parameter, item.valueInt);
                        break;
                    case ParameterType.@float:
                        AnimatorComponent.SetFloat(parameter, item.valueFloat);
                        break;
                    case ParameterType.@bool:
                        AnimatorComponent.SetBool(parameter, item.valueBool);
                        break;
                    default:
                        break;
                }
            }
            return State.Success;

        }

        public override void Initialize()
        {
        }

        public static ParameterType Convert(AnimatorControllerParameterType a)
        {
            switch (a)
            {
                case AnimatorControllerParameterType.Float:
                    return ParameterType.@float;
                case AnimatorControllerParameterType.Int:
                    return ParameterType.@int;
                case AnimatorControllerParameterType.Bool:
                    return ParameterType.@bool;
                case AnimatorControllerParameterType.Trigger:
                    return ParameterType.trigger;
            }
            return ParameterType.invalid;
        }
    }
}
