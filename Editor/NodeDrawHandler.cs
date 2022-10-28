using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public class NodeDrawHandler
    {
        public AIEditor editor;
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
        public NodeDrawHandler(AIEditor editor, TreeNode node)
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

            if (node is AIEditor.EditorHeadNode)
            {

            }

            if (tree.IsServiceCall(node))
            {
                EditorGUILayout.LabelField("Service " + NodeDrawerBase.GetEditorName(tree.GetServiceHead(node)), EditorStyles.boldLabel);
            }
            EditorGUILayout.LabelField(NodeDrawerBase.GetEditorName(node), EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            try
            {
                if (node is not null) FindDrawer();
                else GUILayout.Label("No Node (possibly an error)", EditorStyles.boldLabel);

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            //GUILayout.FlexibleSpace();
            EditorGUI.indentLevel--;
        }

        private void FindDrawer()
        {
            if (drawer != null)
            {
                drawer.editor = editor;
                drawer.node = Node;
                drawer.DrawNodeBaseInfo();
                drawer.Draw();
                return;
            }

            drawer = new DefaultNodeDrawer();

            var classes = GetType().Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(NodeDrawerBase)));

            var drawerType = classes.FirstOrDefault(t =>
            {
                Type type = Node.GetType();
                bool v = ((CustomNodeDrawerAttribute)Attribute.GetCustomAttribute(t, typeof(CustomNodeDrawerAttribute)))?.type == type;
                return v;
            });

            if (drawerType != null)
            {
                if (Activator.CreateInstance(drawerType) is NodeDrawerBase newDrawer)
                {
                    drawer = newDrawer;
                }
                else
                {
                    Debug.LogError("drawer not create");
                }
            }
            else if (node is DetermineBase)
            {
                drawer = new DetermineNodeDrawer();
            }


            drawer.editor = editor;
            drawer.node = Node;
            drawer.DrawNodeBaseInfo();
            drawer.Draw();
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