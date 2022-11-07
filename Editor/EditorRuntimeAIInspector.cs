﻿using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// AI editor window
    /// </summary>
    public class EditorRuntimeBehaviourTreeInspector : EditorWindow
    {
        private AI selected;

        bool displayHidden;
        private Vector2 nodeRect;
        private Vector2 varRect;
        private bool publicFoldOut;
        private bool nonpublicFoldOut;

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
        public static EditorRuntimeBehaviourTreeInspector ShowWindow()
        {
            //Show existing window instance. If one doesn't exist, make one.
            var window = GetWindow(typeof(EditorRuntimeBehaviourTreeInspector), false, "AI Inspector");
            window.name = "AI Inspector";
            return window as EditorRuntimeBehaviourTreeInspector;

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
            GUILayout.Toolbar(-1, new string[] { "" });
            if (!selected)
            {
                EditorGUILayout.LabelField("You must select an AI to view AI status");
                return;
            }
            if (!selected.data)
            {
                EditorGUILayout.LabelField("AI do not have a behaviour tree data.");
                return;
            }

            EditorGUILayout.BeginVertical();

            EditorGUILayout.LabelField($"Instance of {selected.data.name}");
            if (selected.behaviourTree == null || !selected.behaviourTree.IsRunning)
            {
                EditorGUILayout.LabelField("AI is not running");
                EditorGUILayout.Space(20);
                EditorGUILayout.LabelField("Head");
                NodeDrawers.DrawNodeBaseInfo(selected.data.Head, true);
            }
            else DrawWindow();

            GUILayout.FlexibleSpace();
            DrawToolbar();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            if (!selected.behaviourTree.IsRunning)
            {
                if (Application.isPlaying)
                {
                    if (GUILayout.Button("Start"))
                    {
                        selected.StartBehaviourTree();
                    }
                }
                else
                {
                    GUILayout.Toolbar(-1, new string[] { "" });
                }
            }
            else
            {
                int index = GUILayout.Toolbar(-1, new string[] { (selected.behaviourTree.IsPaused ? "Continue" : "Pause"), "Restart" });
                switch (index)
                {
                    case 0:
                        if (selected.behaviourTree.IsRunning)
                            if (selected.behaviourTree.IsPaused)
                            {
                                selected.Continue();
                            }
                            else
                            {
                                selected.Pause();
                            }
                        break;
                    case 1:
                        selected.Reload();
                        break;
                    default:
                        break;
                }
            }
        }

        private void DrawWindow()
        {
            EditorGUILayout.BeginHorizontal();

            //node status
            DrawNodeFieldStatus();

            //variables
            DrawVariable();

            EditorGUILayout.EndHorizontal();
        }


        private void DrawVariable()
        {
            BeginVerticleAndSetWindowColor();
            varRect = EditorGUILayout.BeginScrollView(varRect);
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
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawNodeFieldStatus()
        {
            EditorGUILayout.BeginVertical();
            var wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUILayout.LabelField("Current Node");
            if (!displayHidden) if (GUILayout.Button("Display Hidden Field")) displayHidden = true;
            if (displayHidden) if (GUILayout.Button("Hide Hidden Field")) displayHidden = false;
            var node = selected.behaviourTree.CurrentStage;
            NodeDrawers.DrawNodeBaseInfo(node);
            nodeRect = EditorGUILayout.BeginScrollView(nodeRect);
            if (node != null)
            {
                publicFoldOut = EditorGUILayout.Foldout(publicFoldOut, "Public");
                if (publicFoldOut)
                {
                    EditorGUI.indentLevel++;
                    foreach (FieldInfo fieldInfo in node.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        DrawField(node, fieldInfo);
                    }
                    EditorGUI.indentLevel--;
                }
                nonpublicFoldOut = EditorGUILayout.Foldout(nonpublicFoldOut, "Non-Public");
                if (nonpublicFoldOut)
                {
                    EditorGUI.indentLevel++;
                    foreach (FieldInfo fieldInfo in node.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        DrawField(node, fieldInfo);
                    }
                    EditorGUI.indentLevel--;
                }

            }
            EditorGUILayout.EndScrollView();
            GUILayout.FlexibleSpace();
            EditorGUIUtility.wideMode = wideMode;
            EditorGUILayout.EndVertical();
        }

        private void DrawField(TreeNode node, FieldInfo fieldInfo)
        {
            if (fieldInfo.Name == nameof(node.name)) return;
            if (fieldInfo.Name == nameof(node.uuid)) return;
            if (fieldInfo.Name == nameof(node.services)) return;
            if (fieldInfo.Name == nameof(node.behaviourTree)) return;
            var labelName = fieldInfo.Name.ToTitleCase();


            if (Attribute.IsDefined(fieldInfo, typeof(DisplayIfAttribute)) && !displayHidden)
            {
                try
                {
                    if (!DisplayIfAttribute.IsTrue(node, fieldInfo)) return;
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help"); return;
                }
            }

            if (fieldInfo.FieldType.IsSubclassOf(typeof(VariableBase)))
            {
                if (fieldInfo.GetValue(node) is not VariableBase variablefield || variablefield.Value == null)
                {
                    EditorGUILayout.LabelField(labelName, "null");
                }
                else
                {
                    //a constant, can force set its value
                    if (variablefield.IsConstant)
                    {
                        variablefield.ForceSetConstantValue(EditorFieldDrawers.DrawField(labelName, variablefield.Value));
                    }
                    else
                    {
                        // a variable, cannot set variable, change it on variable panel
                        EditorFieldDrawers.DrawField(labelName, variablefield.Value, true);
                    }
                }
            }
            else if (fieldInfo.FieldType == typeof(NodeReference))
            {
                var nodeReference = fieldInfo.GetValue(node) as NodeReference;
                TreeNode referTo = nodeReference.node;
                if (referTo != null)
                {
                    EditorGUILayout.LabelField(labelName, $"{referTo.name} ({referTo.uuid})");
                }
                else EditorGUILayout.LabelField(labelName, "null");
            }
            else
            {
                object value = fieldInfo.GetValue(node);
                if (value != null)
                {
                    fieldInfo.SetValue(node, EditorFieldDrawers.DrawField(labelName, value));
                }
                else
                {
                    EditorGUILayout.LabelField(labelName, "null");
                }
            }
        }

        private void BeginVerticleAndSetWindowColor()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle, GUILayout.MaxWidth(position.width / 3));
            GUI.backgroundColor = baseColor;
        }
    }
}