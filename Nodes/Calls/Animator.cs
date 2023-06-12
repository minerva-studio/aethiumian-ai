using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using UnityEngine;
using Ator = UnityEngine.Animator;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// 
    /// </summary>
    [NodeTip("Change parameters of an animator")]
    [Serializable]
    public sealed class Animator : Call
    {
        [Serializable]
        public class Parameter
        {
            public bool use;

            public string parameter;
            public ParameterType type;

            [DisplayIf(nameof(type), ParameterType.@int)] public VariableField<int> valueInt;
            [DisplayIf(nameof(type), ParameterType.@float)] public VariableField<float> valueFloat;
            [DisplayIf(nameof(type), ParameterType.@bool)] public VariableField<bool> valueBool;
            [DisplayIf(nameof(type), ParameterType.trigger)] public TriggerSet setTrigger;
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