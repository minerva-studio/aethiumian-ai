using Amlos.AI.Nodes;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(AnimationCall))]
    public class AnimationCallDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            AnimationCall animationCall = node as AnimationCall;
            if (!tree.animatorController)
            {
                animationCall.parameter = EditorGUILayout.TextField("Parameter Name", animationCall.parameter);
                animationCall.type = (AnimationCall.ParamterType)EditorGUILayout.EnumPopup("Parameter", animationCall.type);
            }
            else
            {
                var parameters = tree.animatorController.parameters;
                var names = parameters.Select(s => s.name).ToArray();
                int index = Array.IndexOf(names, animationCall.parameter);
                if (index < 0)
                    index = 0;

                index = EditorGUILayout.Popup("Parameter Name", index, names);

                if (names.Length == 0)
                {
                    EditorGUILayout.HelpBox($"Animator {tree.animatorController.name} has no parameter", MessageType.Warning);
                    return;
                }
                // parameter changed
                else if (index == -1)
                {
                    EditorGUILayout.HelpBox($"Parameter {animationCall.parameter} not found on animator {tree.animatorController.name}", MessageType.Warning);
                    return;
                }
                else
                {
                    //Debug.Log(index);
                    animationCall.parameter = names[index];
                    animationCall.type = AnimationCall.Convert(
                        parameters.First(p => p.name == animationCall.parameter).type
                    );
                }
            }
            switch (animationCall.type)
            {
                case AnimationCall.ParamterType.@int:
                    DrawVariable("Value", animationCall.valueInt);
                    break;
                case AnimationCall.ParamterType.@float:
                    DrawVariable("Value", animationCall.valueFloat);
                    break;
                case AnimationCall.ParamterType.@bool:
                    DrawVariable("Value", animationCall.valueBool);
                    break;
                case AnimationCall.ParamterType.trigger:
                    animationCall.setTrigger = (AnimationCall.TriggerSet)
                        EditorGUILayout.EnumPopup("Trigger", animationCall.setTrigger);
                    break;
                default:
                    break;
            }
        }
    }
}
