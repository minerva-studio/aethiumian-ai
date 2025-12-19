using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
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
    public class AIInspector : EditorWindow
    {
        private AI selected;

        bool displayHidden;
        private Vector2 nodeRect;
        private Vector2 varRect;

        private enum LayoutMode { Horizontal, Vertical }
        [SerializeField] private LayoutMode layoutMode = LayoutMode.Horizontal;

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

        [MenuItem("Window/Aethiumian AI/AI Runtime Inspector")]
        public static AIInspector ShowWindow()
        {
            var window = GetWindow(typeof(AIInspector), false, "AI Inspector");
            window.name = "AI Inspector";
            return window as AIInspector;
        }

        private void Update()
        {
            Repaint();
        }

        private void OnGUI()
        {
            var wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(MonoScript.FromScriptableObject(this), typeof(MonoScript), false);

            Draw();
            EditorGUIUtility.wideMode = wideMode;
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
                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.LabelField("Head");
                NodeDrawerUtility.DrawNodeBaseInfo(selected.data, selected.data.Head, true);
            }
            else DrawWindow();

            GUILayout.FlexibleSpace();
            DrawToolbar();
            EditorGUILayout.EndVertical();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // Layout mode toggle
                var newLayoutIndex = GUILayout.Toolbar((int)layoutMode, new[] { "Horizontal", "Vertical" }, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (newLayoutIndex != (int)layoutMode)
                {
                    layoutMode = (LayoutMode)newLayoutIndex;
                }

                GUILayout.FlexibleSpace();
            }

            if (!selected.behaviourTree.IsRunning)
            {
                if (!Application.isPlaying)
                {
                    GUILayout.Toolbar(-1, new string[] { "" });
                }
                else if (GUILayout.Button("Start"))
                {
                    selected.StartBehaviourTree();
                }
            }
            else
            {
                int index = GUILayout.Toolbar(-1, new string[] { (selected.behaviourTree.IsPaused ? "Continue" : "Pause"), "Restart" });
                switch (index)
                {
                    case 0:
                        if (!selected.behaviourTree.IsRunning)
                            break;
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
            if (layoutMode == LayoutMode.Horizontal)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawNodeFieldStatus();
                    DrawVariable();
                }
            }
            else
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawNodeFieldStatus();
                    EditorGUILayout.Space(6);
                    DrawVariable();
                }
            }
        }

        private void DrawVariable()
        {
            BeginVerticleAndSetWindowColor();
            EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);
            using (EditorGUIIndent.Increase)
            using (new GUIScrollView(ref varRect))
            {
                EditorGUILayout.LabelField("Instance", EditorStyles.boldLabel);
                DrawVariableTable(selected.behaviourTree.EditorVariables);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Static", EditorStyles.boldLabel);
                DrawVariableTable(selected.behaviourTree.EditorStaticVariables);
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Global", EditorStyles.boldLabel);
                DrawVariableTable(BehaviourTree.EditorGlobalVariables);
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawVariableTable(VariableTable table)
        {
            GUILayout.BeginVertical();
            foreach (var variable in table)
            {
                if (variable is null) continue;
                var newVal = EditorFieldDrawers.DrawField(variable.Name.ToTitleCase(), variable.Value, variable.ObjectType);
                if (variable.Value == null) continue;
                if (!variable.Value.Equals(newVal))
                {
                    variable.SetValue(newVal);
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawNodeFieldStatus()
        {
            var wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            using (EditorGUIIndent.Increase)
            using (new EditorGUILayout.VerticalScope())
            {
                if (!displayHidden) if (GUILayout.Button("Display Hidden Field")) displayHidden = true;
                if (displayHidden) if (GUILayout.Button("Hide Hidden Field")) displayHidden = false;

                selected.behaviourTree.Debugging = EditorGUILayout.Toggle("Debug", selected.behaviourTree.Debugging);
                if (selected.behaviourTree.IsRunning && selected.behaviourTree.MainStack != null)
                {
                    selected.behaviourTree.MainStack.IsPaused = EditorGUILayout.Toggle("Pause", selected.behaviourTree.MainStack.IsPaused);
                }

                var node = selected.behaviourTree.CurrentStage.Node;
                if (node != null)
                {
                    EditorGUILayout.Space(20);
                    EditorGUILayout.LabelField("Current Node");
                    NodeDrawerUtility.DrawNodeBaseInfo(selected.data, node);
                    nodeRect = EditorGUILayout.BeginScrollView(nodeRect);

                    foreach (var fieldInfo in GetAllFields(node.GetType(), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        DrawMember(node, fieldInfo);
                    }

                    EditorGUILayout.EndScrollView();
                }

                GUILayout.FlexibleSpace();
            }
            EditorGUIUtility.wideMode = wideMode;
        }

        private void DrawMember(TreeNode node, FieldInfo fieldInfo)
        {
            if (fieldInfo.Name == nameof(node.name)) return;
            if (fieldInfo.Name == nameof(node.uuid)) return;
            if (fieldInfo.Name == nameof(node.services)) return;
            if (fieldInfo.Name == nameof(node.behaviourTree)) return;

            if (IsDelegateLike(fieldInfo.FieldType)) return;

            var property = ResolveAutoProperty(fieldInfo);
            if (property != null && Attribute.IsDefined(property, typeof(Amlos.AI.AIInspectorIgnoreAttribute), true)) return;

            if (Attribute.IsDefined(fieldInfo, typeof(Amlos.AI.AIInspectorIgnoreAttribute), true)) return;

            if (Attribute.IsDefined(fieldInfo, typeof(DisplayIfAttribute)) && !displayHidden)
            {
                try
                {
                    if (!ConditionalFieldAttribute.IsTrue(node, fieldInfo)) return;
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(NormalizeFieldLabel(fieldInfo).ToTitleCase(), "DisplayIf attribute breaks, ask for help");
                    return;
                }
            }

            DrawField(node, fieldInfo);
        }

        private void DrawField(TreeNode node, FieldInfo fieldInfo)
        {
            var labelName = NormalizeFieldLabel(fieldInfo).ToTitleCase();

            var value = fieldInfo.GetValue(node);
            if (value is VariableBase variablefield)
            {
                VariableFieldDrawers.DrawVariable("[Var] " + labelName, variablefield, selected.data);
            }
            else if (value is INodeReference nodeReference)
            {
                TreeNode referTo = nodeReference.Node;
                if (referTo != null)
                {
                    EditorGUILayout.LabelField(labelName, $"Node {referTo.name} ({referTo.uuid})");
                }
                else EditorGUILayout.LabelField(labelName, "Node (null)");
            }
            else if (value != null)
            {
                fieldInfo.SetValue(node, EditorFieldDrawers.DrawField(labelName, value, fieldInfo.FieldType));
            }
            else
            {
                EditorGUILayout.LabelField(labelName, "null");
            }
        }

        private void BeginVerticleAndSetWindowColor()
        {
            var colorStyle = new GUIStyle();
            colorStyle.normal.background = Texture2D.whiteTexture;
            var baseColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(64 / 255f, 64 / 255f, 64 / 255f);
            GUILayout.BeginVertical(colorStyle);
            GUI.backgroundColor = baseColor;
        }

        internal void Load(AI ai)
        {
            selected = ai;
        }

        private static System.Collections.Generic.IEnumerable<FieldInfo> GetAllFields(Type type, BindingFlags flags)
        {
            var list = new System.Collections.Generic.List<FieldInfo>();
            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                var levelFlags = flags | BindingFlags.DeclaredOnly;
                var fields = t.GetFields(levelFlags);
                if (fields != null && fields.Length > 0)
                {
                    list.AddRange(fields);
                }
            }
            list.Reverse();
            return list;
        }

        private static PropertyInfo ResolveAutoProperty(FieldInfo fieldInfo)
        {
            var name = fieldInfo.Name;
            if (string.IsNullOrEmpty(name)) return null;
            if (!name.StartsWith("<", StringComparison.Ordinal) || !name.EndsWith(">k__BackingField", StringComparison.Ordinal))
            {
                return null;
            }

            var propName = name.Substring(1, name.Length - 1 - ">k__BackingField".Length);
            var declaringType = fieldInfo.DeclaringType;
            if (declaringType == null) return null;

            var property = declaringType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance)
                           ?? declaringType.GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Instance);
            return property;
        }

        private static string NormalizeFieldLabel(FieldInfo fieldInfo)
        {
            var property = ResolveAutoProperty(fieldInfo);
            if (property != null) return property.Name;
            return fieldInfo.Name;
        }

        private static bool IsDelegateLike(Type type)
        {
            return type != null && typeof(Delegate).IsAssignableFrom(type);
        }
    }
}