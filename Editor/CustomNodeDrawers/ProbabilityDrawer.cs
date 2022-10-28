﻿using Amlos.AI;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Amlos.Editor.AIEditor;
using Minerva.Module;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(Probability))]
    public class ProbabilityDrawer : NodeDrawerBase
    {
        public override void Draw()
        {
            if (this.node is not Probability probability) return;
            DrawProbabilityWeightList(nameof(Probability), probability, probability.events);

        }

        private void DrawProbabilityWeightList(string listName, TreeNode propNode, List<Probability.EventWeight> list)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            int totalWeight = list.Sum();
            for (int i = 0; i < list.Count; i++)
            {
                Probability.EventWeight eventWeight = list[i];
                var item = eventWeight.reference;
                var childNode = tree.GetNode(item);
                GUILayout.BeginHorizontal();
                DrawListItemCommonModify(list, i);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                if (childNode == null)
                {
                    var currentColor = GUI.contentColor;
                    GUI.contentColor = Color.red;
                    GUILayout.Label("Node not found: " + item);
                    GUI.contentColor = currentColor;
                }
                else
                {
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField($"{GetEditorName(childNode)} ({(eventWeight.weight / (float)totalWeight).ToString("0.0%")})");
                    EditorGUI.indentLevel++;

                    childNode.name = EditorGUILayout.TextField("Name", childNode.name);
                    int weight = EditorGUILayout.IntField("Weight", list[i].weight);
                    list[i].SetWeight(weight < 0 ? 0 : weight);
                    EditorGUILayout.LabelField("UUID", childNode.uuid);

                    EditorGUI.indentLevel--;
                    GUILayout.EndVertical();
                }
                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.BeginVertical();
            GUILayout.Label("Ratio: ");
            GUILayout.BeginHorizontal();
            var rect = EditorGUILayout.GetControlRect();
            var areaSizeX = rect.width;
            var newX = rect.x;
            foreach (var eventWeight in list)
            {
                var item = eventWeight.reference;
                rect.x = newX;
                rect.width = (eventWeight.weight / (float)totalWeight) * areaSizeX;
                newX += rect.width;
                var childNode = tree.GetNode(item);
                if (childNode != null)
                    GUI.Button(rect, $"{childNode.name} ({(eventWeight.weight / (float)totalWeight).ToString("0.0%")})");
                else
                    GUI.Button(rect, $"{"Unknown"} ({(eventWeight.weight / (float)totalWeight).ToString("0.0%")})");
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.nodeType, (n) =>
                {
                    list.Add(new Probability.EventWeight() { reference = n, weight = 1 });
                    n.parent  = propNode;
                });
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

    }

}