using Amlos.AI.Variables;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{

    /// <summary>
    /// 
    /// </summary>
    [NodeTip("Change parameters of an animator")]
    [Serializable]
    public sealed class AnimationCall : Call
    {
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

        //public VariableField<int> intField;
        //public VariableField<float> floatField;
        //public VariableField<string> stringField;
        //public VariableField<bool> boolField;
        //public VariableField<ParamterType> enumField;
        [DisplayIf(nameof(type), ParamterType.@int)] public VariableField<int> valueInt;
        [DisplayIf(nameof(type), ParamterType.@float)] public VariableField<float> valueFloat;
        [DisplayIf(nameof(type), ParamterType.@bool)] public VariableField<bool> valueBool;
        [DisplayIf(nameof(type), ParamterType.trigger)] public TriggerSet setTrigger;

        Animator animator;
        Animator Animator => animator ? animator : animator = gameObject.GetComponent<Animator>();

        public override void Execute()
        {
            //AddSelfToProgress();
            if (!Animator)
            {
                End(false);
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
            End(true);

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
    }
}