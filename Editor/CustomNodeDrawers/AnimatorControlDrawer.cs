using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Nodes.Animator))]
    public class AnimatorControlDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            Nodes.Animator ac = node as Nodes.Animator;

            var parameters = TreeData.animatorController.parameters;
            foreach (var item in parameters)
            {
                var p = ac.parameters.FirstOrDefault(p => p.parameter == item.name);
                if (p == null)
                {
                    p = new Nodes.Animator.Parameter();
                    ac.parameters.Add(p);
                }

                GUILayout.BeginHorizontal();
                var enabled = GUI.enabled;
                p.use = EditorGUILayout.Toggle(p.use, GUILayout.Width(EditorGUIUtility.singleLineHeight * 2));
                p.parameter = item.name;
                p.type = Nodes.Animator.Convert(item.type);
                var displayName = item.name.ToTitleCase();

                GUI.enabled = p.use;
                switch (p.type)
                {
                    case Nodes.Animator.ParameterType.@int:
                        DrawVariable(displayName, p.valueInt);
                        break;
                    case Nodes.Animator.ParameterType.@float:
                        DrawVariable(displayName, p.valueFloat);
                        break;
                    case Nodes.Animator.ParameterType.@bool:
                        DrawVariable(displayName, p.valueBool);
                        break;
                    case Nodes.Animator.ParameterType.trigger:
                        p.setTrigger = (Nodes.Animator.TriggerSet)
                            EditorGUILayout.EnumPopup(displayName, p.setTrigger);
                        break;
                    default:
                        break;
                }
                GUI.enabled = enabled;
                GUILayout.EndHorizontal();

            }
        }
    }
}
