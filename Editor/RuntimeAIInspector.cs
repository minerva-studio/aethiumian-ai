using Minerva.Module;
using Minerva.Module.Editor;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// AI editor window
    /// </summary>
    public class RuntimeAIInspector : EditorWindow
    {
        private AI selected;

        private void OnValidate()
        {
            SelectGameObject();
        }

        private void SelectGameObject()
        {
            var newSelected = Selection.activeGameObject;
            if (!newSelected) return;
            if (newSelected.GetComponent<AI>() is not AI aI) return;
            if (!selected) selected = aI;
            else
            {
                selected = aI;
            }
        }

        // Add menu item named "My Window" to the Window menu
        [MenuItem("Window/AI Runtime Inspector")]
        public static RuntimeAIInspector ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            var window = GetWindow(typeof(RuntimeAIInspector), false, "AI Inspector");
            window.name = "AI Inspector";
            return window as RuntimeAIInspector;

        }


        private void Update()
        {
            Repaint();
        }
        private void OnGUI()
        {
            Draw();
        }

        private void Draw()
        {
            SelectGameObject();
            if (!selected)
            {
                EditorGUILayout.LabelField("You must select an AI to view AI status");
                return;
            }
            if (selected.behaviourTree == null || !selected.behaviourTree.IsRunning)
            {
                EditorGUILayout.LabelField("AI is not running");
                return;
            }

            DrawWindow();
        }

        private void DrawWindow()
        {
            EditorGUILayout.BeginHorizontal();

            //node status
            DrawNodeStatus();

            //variables
            DrawVariable();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawVariable()
        {
            BeginVerticleAndSetWindowColor();
            EditorGUILayout.LabelField("Variables");
            foreach (var item in selected.behaviourTree.Variables)
            {
                var variable = item.Value;
                var newVal = EditorFieldDrawers.DrawField(variable.Name.ToTitleCase(), variable.Value);
                if (!variable.Value.Equals(newVal)) //make sure it is value-equal, not reference equal
                {
                    variable.SetValue(newVal);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawNodeStatus()
        {
            EditorGUILayout.BeginVertical();
            var node = selected.behaviourTree.CurrentStage;
            if (node != null)
            {
                var GUIState = GUI.enabled;
                GUI.enabled = false;
                var wideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = true;


                EditorGUILayout.LabelField("Public:");
                foreach (FieldInfo fieldInfo in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    DrawField(node, fieldInfo);
                }

                EditorGUILayout.Space(50);
                EditorGUILayout.LabelField("Non-Public:");
                foreach (FieldInfo fieldInfo in node.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    DrawField(node, fieldInfo);
                }

                EditorGUIUtility.wideMode = wideMode;
                GUI.enabled = GUIState;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawField(TreeNode node, FieldInfo fieldInfo)
        {
            if (fieldInfo.Name == nameof(node.name)) return;
            if (fieldInfo.Name == nameof(node.services)) return;
            if (fieldInfo.Name == nameof(node.behaviourTree)) return;


            if (fieldInfo.FieldType.IsSubclassOf(typeof(VariableBase)))
            {
                var variablefield = fieldInfo.GetValue(node) as VariableBase;

                if (variablefield != null && variablefield.Value != null)
                {
                    EditorFieldDrawers.DrawField(fieldInfo.Name.ToTitleCase(), variablefield.Value);
                }
                else EditorGUILayout.LabelField(fieldInfo.Name.ToTitleCase(), "null");
            }
            else if (fieldInfo.FieldType == typeof(NodeReference))
            {
                var nodeReference = fieldInfo.GetValue(node) as NodeReference;
                TreeNode referTo = nodeReference.node;
                if (referTo != null)
                {
                    EditorGUILayout.LabelField(fieldInfo.Name.ToTitleCase(), $"{referTo.name}({referTo.uuid})");
                }
                else EditorGUILayout.LabelField(fieldInfo.Name.ToTitleCase(), "null");
            }
            else
            {
                object value = fieldInfo.GetValue(node);
                if (value != null) EditorFieldDrawers.DrawField(fieldInfo.Name, value);
                else EditorGUILayout.LabelField(fieldInfo.Name.ToTitleCase(), "null");
            }
        }

        private void BeginVerticleAndSetWindowColor()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle, GUILayout.MinHeight(position.height - 130));
            GUI.backgroundColor = baseColor;
        }
    }
}