using Amlos.AI.Nodes;
using System;
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
        public AIEditorWindow editor;
        public NodeDrawerBase drawer;
        private TreeNode node;

        protected BehaviourTreeData tree => editor.tree;

        public TreeNode Node
        {
            get => node; set
            {
                node = value;
                if (drawer != null)
                {
                    drawer.node = value;
                }
            }
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

            if (tree.IsServiceCall(node))
            {
                GUILayout.Label("Service " + NodeDrawers.GetEditorName(tree.GetServiceHead(node)), EditorStyles.boldLabel);
            }
            GUILayout.Label(NodeDrawers.GetEditorName(node), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                if (node is not null)
                {
                    if (drawer == null) FindDrawer();
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
        private void FindDrawer()
        {
            drawer = new DefaultNodeDrawer();

            var classes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes().Where(t => t.IsSubclassOf(typeof(NodeDrawerBase))));

            var drawerType = classes.FirstOrDefault(t =>
            {
                Type type = Node.GetType();
                Type attributeServingType = ((CustomNodeDrawerAttribute)Attribute.GetCustomAttribute(t, typeof(CustomNodeDrawerAttribute)))?.type;
                bool v = attributeServingType != null && (attributeServingType == type || type.IsSubclassOf(attributeServingType));
                return v;
            });

            if (drawerType == null)
            {
                if (node is DetermineBase)
                {
                    drawer = new DetermineNodeDrawer();
                }
            }
            else if (Activator.CreateInstance(drawerType) is NodeDrawerBase newDrawer)
            {
                drawer = newDrawer;
            }
            else
            {
                Debug.LogError("drawer not create");
            }
        }

        private void Draw_Internal()
        {
            var mode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            drawer.editor = editor;
            drawer.node = Node;
            drawer.DrawNodeBaseInfo();
            drawer.Draw();

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
    }

}