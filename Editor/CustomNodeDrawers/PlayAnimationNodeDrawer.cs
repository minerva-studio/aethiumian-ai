using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Nodes.PlayAnimation))]
    public class PlayAnimationNodeDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (!TreeData.animatorController)
            {
                EditorGUILayout.HelpBox($"Animator of the AI {TreeData.name} has not yet been assigned", MessageType.Warning);
                return;
            }

            List<string> states = new();
            List<int> stateLayers = new();
            foreach (var layer in TreeData.animatorController.layers)
            {
                foreach (var item in layer.stateMachine.states)
                {
                    states.Add(layer.name + "." + item.state.name);
                    stateLayers.Add(layer.syncedLayerIndex);
                }
            }

            // no parameter
            if (states.Count == 0)
            {
                EditorGUILayout.HelpBox($"Animator {TreeData.animatorController.name} has no state", MessageType.Warning);
                return;
            }

            Nodes.PlayAnimation ac = node as Nodes.PlayAnimation;

            var selections = states.ToArray();
            var index = Array.IndexOf(selections, ac.stateName.StringValue);
            index = EditorGUILayout.Popup("State", index, selections);
            if (index != -1)
            {
                ac.stateName.ForceSetConstantValue(selections[index]);
                ac.layer.ForceSetConstantValue(stateLayers[index]);
            }
        }
    }
}
