using Amlos.AI.Variables;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Old animator call, now update to <see cref="Amlos.AI.Nodes.Animator"/>
    /// </summary>
    [NodeTip("Change single parameter of an animator")]
    [Serializable]
    [DoNotRelease]
    public sealed class AnimationCall : Call
    {
        public class AnimationParameter
        {
            public string parameter;
            public ParamterType type;

            [DisplayIf(nameof(type), ParamterType.@int)] public VariableField<int> valueInt;
            [DisplayIf(nameof(type), ParamterType.@float)] public VariableField<float> valueFloat;
            [DisplayIf(nameof(type), ParamterType.@bool)] public VariableField<bool> valueBool;
            [DisplayIf(nameof(type), ParamterType.trigger)] public TriggerSet setTrigger;
        }


        public enum ParamterType
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


        public string parameter;
        public ParamterType type;


        [DisplayIf(nameof(type), ParamterType.@int)] public VariableField<int> valueInt;
        [DisplayIf(nameof(type), ParamterType.@float)] public VariableField<float> valueFloat;
        [DisplayIf(nameof(type), ParamterType.@bool)] public VariableField<bool> valueBool;
        [DisplayIf(nameof(type), ParamterType.trigger)] public TriggerSet setTrigger;



        UnityEngine.Animator animator;
        UnityEngine.Animator Animator => animator ? animator : animator = gameObject.GetComponent<UnityEngine.Animator>();



        public override State Execute()
        {
            //AddSelfToProgress();
            if (!Animator)
            {
                return State.Failed;
            }
            switch (type)
            {
                case ParamterType.trigger:
                    if (setTrigger == TriggerSet.set) Animator.SetTrigger(parameter);
                    else Animator.ResetTrigger(parameter);
                    break;
                case ParamterType.@int:
                    Animator.SetInteger(parameter, valueInt);
                    break;
                case ParamterType.@float:
                    Animator.SetFloat(parameter, valueFloat);
                    break;
                case ParamterType.@bool:
                    Animator.SetBool(parameter, valueBool);
                    break;
                default:
                    break;
            }
            return State.Success;

        }

        public static ParamterType Convert(AnimatorControllerParameterType a)
        {
            switch (a)
            {
                case AnimatorControllerParameterType.Float:
                    return ParamterType.@float;
                case AnimatorControllerParameterType.Int:
                    return ParamterType.@int;
                case AnimatorControllerParameterType.Bool:
                    return ParamterType.@bool;
                case AnimatorControllerParameterType.Trigger:
                    return ParamterType.trigger;
            }
            return ParamterType.invalid;
        }


#if UNITY_EDITOR 
        public override TreeNode Upgrade()
        {
            var animator = new Animator()
            {
                name = this.name,
                uuid = this.uuid,
                parent = this.parent,
                parameters = new System.Collections.Generic.List<Animator.Parameter> {
                    new Animator.Parameter() {
                        use = true,
                        parameter = parameter,
                        type = (Animator.ParameterType)(int)type,
                        valueInt = valueInt,
                        valueFloat = valueFloat,
                        valueBool = valueBool,
                        setTrigger = (Animator.TriggerSet)(int)setTrigger
                    }
                }
            };
            return animator;
        }

#endif
    }
}
