using Amlos.AI.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Node drawer handler
    /// </summary>
    public class NodeDrawHandler
    {
        private static Dictionary<Type, Type> drawers;

        private AIEditorWindow editor;
        private NodeDrawerBase drawer;
        private TreeNode node;

        public TreeNode Node
        {
            get => node;
        }

        public NodeDrawHandler() { }
        public NodeDrawHandler(AIEditorWindow editor, TreeNode node)
        {
            this.editor = editor;
            this.node = node;
        }



        public void Draw()
        {
            if (node is null)
            {
                return;
            }

            FillNullField(node);

            if (editor.tree.IsServiceCall(node))
            {
                GUILayout.Label("Service " + NodeDrawerUtility.GetEditorName(editor.tree.GetServiceHead(node)), EditorStyles.boldLabel);
            }
            GUILayout.Label(NodeDrawerUtility.GetEditorName(node), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                if (node is not null)
                {
                    drawer ??= FindDrawer();
                    Draw_Internal();
                }
                else GUILayout.Label("Given node is null (possibly an error)", EditorStyles.boldLabel);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            //GUILayout.FlexibleSpace();
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Find the drawer
        /// </summary>  
        private NodeDrawerBase FindDrawer()
        {
            drawers ??= GetNodeDrawers();
            drawer = new DefaultNodeDrawer();
            var drawerType = GetNodeDrawer();
            if (drawerType != null)
            {
                if (Activator.CreateInstance(drawerType) is NodeDrawerBase newDrawer)
                {
                    return drawer = newDrawer;
                }
                Debug.LogError("drawer not create");
            }
            else
            {
                if (node is DetermineBase)
                {
                    return drawer = new DetermineNodeDrawer();
                }
            }
            return drawer;
        }

        private void Draw_Internal()
        {
            var mode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            SerializedProperty serializedProperty = editor.tree.GetNodeProperty(node);
            drawer.Init(editor, serializedProperty);
            drawer.DrawNodeBaseInfo();
            drawer.Draw();
            if (serializedProperty.serializedObject.hasModifiedProperties)
            {
                serializedProperty.serializedObject.ApplyModifiedProperties();
                serializedProperty.serializedObject.Update();
            }

            if (!node.EditorCheck(editor.tree))
            {
                EditorGUILayout.HelpBox($"{node.GetType().Name} \"{node.name}\" has some error.", MessageType.Error);
            }

            EditorGUIUtility.wideMode = mode;
        }

        private void FillNullField(TreeNode node)
        {
            var type = node.GetType();
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                if (field.Name == nameof(node.behaviourTree)) continue;
                var fieldType = field.FieldType;
                if (fieldType.IsSubclassOf(typeof(UnityEngine.Object))) continue;
                //Null Determine
                if (fieldType.IsClass && field.GetValue(node) is null)
                {
                    try
                    {
                        field.SetValue(node, Activator.CreateInstance(fieldType));
                    }
                    catch (Exception)
                    {
                        field.SetValue(node, default);
                        Debug.LogWarning("Field " + field.Name + " has not initialized yet. Provide this information if there are bugs");
                    }
                }

            }
        }

        public Type GetCurrentDrawerType()
        {
            return drawer?.GetType();
        }

        private Type GetNodeDrawer()
        {
            Type nodeType = node.GetType();
            while (nodeType != typeof(TreeNode))
            {
                if (drawers.TryGetValue(nodeType, out var drawerType))
                {
                    return drawerType;
                }
                nodeType = nodeType.BaseType;
            }
            return null;
        }

        private static Dictionary<Type, Type> GetNodeDrawers()
        {
            return drawers = TypeCache.GetTypesWithAttribute<CustomNodeDrawerAttribute>()
                .ToDictionary(
                    t => ((CustomNodeDrawerAttribute)Attribute.GetCustomAttribute(t, typeof(CustomNodeDrawerAttribute))).type,
                    t => t
                );
        }
    }

}
