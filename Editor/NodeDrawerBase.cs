using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Editor.AIEditor;

namespace Amlos.AI.Editor
{
    public abstract class NodeDrawerBase
    {
        enum IntervalMode
        {
            frame,
            realTime
        }

        public AIEditor editor;
        public TreeNode node;
        private IntervalMode intervalMode;
        private float time;
        private ComponentReferenceDrawer CRDrawer;

        protected BehaviourTreeData tree => editor.tree;
        public TreeNode Node => node;


        public NodeDrawerBase() { }
        public NodeDrawerBase(AIEditor editor, TreeNode node)
        {
            this.editor = editor;
            this.node = node;
        }

        public abstract void Draw();




        public void DrawNodeBaseInfo() => NodeDrawers.DrawNodeBaseInfo(node);


        protected void DrawServiceBase(Service service)
        {
            EditorGUILayout.LabelField("Type", node.GetType().Name);
            intervalMode = (IntervalMode)EditorGUILayout.EnumPopup("Interval Mode", intervalMode);
            switch (intervalMode)
            {
                case IntervalMode.frame:
                    service.interval = EditorGUILayout.IntField("Interval", service.interval);
                    if (service.interval < 0) service.interval = 0;
                    time = service.interval / 60f;
                    break;
                case IntervalMode.realTime:
                    time = EditorGUILayout.FloatField("Time", time);
                    if (time < 0) time = 0;
                    service.interval = (int)(time * 60);
                    break;
                default:
                    break;
            }
            service.randomDeviation = EditorFieldDrawers.DrawRangeField("Deviation", service.randomDeviation);
        }


        protected void DrawField(TreeNode target, FieldInfo field, string labelName)
        {
            Type fieldType = field.FieldType;

            //Null Determine
            if (fieldType.IsClass && field.GetValue(target) is null)
            {
                try
                {
                    field.SetValue(target, Activator.CreateInstance(fieldType));
                }
                catch (Exception)
                {
                    field.SetValue(target, default);
                    Debug.LogWarning("Field " + field.Name + " has not initialized yet. Provide this information if there are bugs");
                }
            }

            //special case
            if (fieldType.IsSubclassOf(typeof(VariableBase)))
            {
                VariableBase variableFieldBase = (VariableBase)field.GetValue(target);
                var possibleType = variableFieldBase.GetVariableTypes(field);
                DrawVariable(labelName, variableFieldBase, possibleType);
            }
            else if (fieldType == typeof(NodeReference))
            {
                DrawNodeSelection(labelName, (NodeReference)field.GetValue(target));
            }
            else if (fieldType == typeof(ComponentReference))
            {
                DrawComponent(labelName, (ComponentReference)field.GetValue(target));
            }
            else if (fieldType.IsSubclassOf(typeof(AssetReferenceBase)))
            {
                DrawAssetReference(labelName, (AssetReferenceBase)field.GetValue(target));
            }
            else if (fieldType == typeof(NodeReference))
            {
                NodeReference reference = (NodeReference)field.GetValue(target);
                EditorGUILayout.LabelField(labelName, reference.ToString());
                DrawNodeSelection(labelName, tree.GetNode(reference));
            }
            else if (fieldType == typeof(List<NodeReference>))
            {
                var list = (List<NodeReference>)field.GetValue(target);
                DrawNodeList(labelName, list, target);
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = field.GetValue(target) as IList;
                DrawList(labelName, list);
            }
            else EditorFieldDrawers.DrawField(labelName, field, target);
        }




        private void DrawComponent(string labelName, ComponentReference componentReference)
        {
            CRDrawer ??= new ComponentReferenceDrawer(componentReference, labelName);
            CRDrawer.Reset(componentReference, labelName);
            CRDrawer.Draw();
        }


        public void DrawAssetReference(string labelName, AssetReferenceBase assetReferenceBase)
        {
            UnityEngine.Object currentAsset = tree.GetAsset(assetReferenceBase.uuid);
            var newAsset = EditorGUILayout.ObjectField(labelName, currentAsset, assetReferenceBase.GetAssetType(), false);
            //asset change
            if (newAsset != currentAsset)
            {
                tree.AddAsset(newAsset);
                assetReferenceBase.SetReference(newAsset);
            }
        }

        protected void DrawVariable(string labelName, VariableBase variable, VariableType[] possibleTypes = null)
        {
            VariableDrawers.DrawVariable(labelName, tree, variable, possibleTypes);
        }


        [Obsolete]
        protected void DrawNodeList(string listName, List<UUID> list, TreeNode node)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                UUID item = list[i];
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
                    EditorGUILayout.LabelField(NodeDrawers.GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    NodeDrawers.DrawNodeBaseInfo(childNode);
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
            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.all, (n) =>
                {
                    list.Add(n.uuid);
                    n.parent = node;
                });
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        protected void DrawNodeList(string listName, List<RawNodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];
                var childNode = tree.GetNode(item.uuid);
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
                    EditorGUILayout.LabelField(NodeDrawers.GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    NodeDrawers.DrawNodeBaseInfo(childNode);
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
            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.all, (n) =>
                {
                    list.Add(n);
                    n.parent = node;
                });
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        protected void DrawNodeList(string listName, List<NodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];
                var childNode = tree.GetNode(item.uuid);
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
                    EditorGUILayout.LabelField(NodeDrawers.GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    NodeDrawers.DrawNodeBaseInfo(childNode);
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
            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.all, (n) =>
                {
                    n.parent = node;
                    list.Add(n);
                });
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }
        protected void DrawList(string listName, IList list)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                GUILayout.BeginHorizontal();
                DrawListItemCommonModify(list, i);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                list[i] = EditorFieldDrawers.DrawField(i.ToString(), item);

                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                Type type = list.GetType().GenericTypeArguments[0];
                if (list.Count > 0) list.Add(list[list.Count - 1]);
                else if (type.IsValueType) list.Add(default);
                else if (type == typeof(string)) list.Add(string.Empty);
                else
                {
                    try
                    {
                        list.Add(Activator.CreateInstance(type));
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Failed to initalized the list's first element, try edit this in inspector first");
                    }
                }
            }
            if (list.Count != 0)
                if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }



        protected void DrawNodeSelection(string labelName, NodeReference reference)
        {
            reference.node = tree.GetNode(reference.uuid);
            TreeNode referencingNode = reference.node;
            string nodeName = referencingNode?.name ?? string.Empty;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(labelName + ": " + nodeName);
            EditorGUI.indentLevel++;
            if (referencingNode is null)
            {
                if (GUILayout.Button("Select.."))
                    editor.OpenSelectionWindow(RightWindow.all, SelectEvent);
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DrawNodeListItemCommonModify(referencingNode, SelectEvent);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                NodeDrawers.DrawNodeBaseInfo(referencingNode);
                EditorGUI.indentLevel = oldIndent;
            }
            EditorGUI.indentLevel--;
            GUILayout.EndHorizontal();


            void SelectEvent(TreeNode n)
            {
                reference.node = n;
                if (n is not null)
                {
                    reference.uuid = n.uuid;
                    n.parent = node;
                    //Debug.Log(reference.uuid);
                }
                else
                {
                    reference.uuid = UUID.Empty;
                }
            }
        }

        protected void DrawNodeListItemCommonModify(TreeNode node, SelectNodeEvent assignmentEvent)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(80));
            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.BeginVertical(GUILayout.MaxWidth(80));
            if (GUILayout.Button("Open"))
            {
                Debug.Log("Open");
                editor.SelectedNode = node;
            }
            else if (GUILayout.Button("Replace"))
            {
                editor.OpenSelectionWindow(RightWindow.all, (n) => { node.parent = NodeReference.Empty; assignmentEvent(n); });
            }
            else if (GUILayout.Button("Delete"))
            {
                assignmentEvent(null);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        protected void DrawListItemCommonModify<T>(List<T> list, int index)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                Debug.Log("Delete");
                T t = list[index];
                list.RemoveAt(index);
                if (t is UUID uuid) tree.GetNode(uuid).parent = NodeReference.Empty;
                else if (t is TreeNode n) n.parent = NodeReference.Empty;
                else if (t is NodeReference nr) { nr.uuid = UUID.Empty; nr.node = null; }
                else if (t is Probability.EventWeight w) tree.GetNode(w.reference).parent = NodeReference.Empty;
                else Debug.Log("Cannot determine list type " + t.GetType().Name);
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(60)))
            {
                T t = list[index];
                if (t is UUID uuid) editor.SelectedNode = tree.GetNode(uuid);
                else if (t is TreeNode n) editor.SelectedNode = n;
                else if (t is NodeReference nr) editor.SelectedNode = tree.GetNode(nr.uuid);
                else if (t is Probability.EventWeight w) editor.SelectedNode = tree.GetNode(w.reference);
                else Debug.Log("Cannot determine list type " + t.GetType().Name);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                return;
            }
            var item = list[index];

            var currentStatus = GUI.enabled;
            if (index == 0) GUI.enabled = false;
            if (GUILayout.Button("Up"))
            {
                list.Remove(item);
                list.Insert(index - 1, item);
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                list.Remove(item);
                list.Insert(index + 1, item);
            }
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        protected void DrawListItemCommonModify(IList list, int index)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                Debug.Log("Delete");
                list.RemoveAt(index);
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16); 
            var item = list[index];

            var currentStatus = GUI.enabled;
            if (index == 0) GUI.enabled = false;
            if (GUILayout.Button("Up"))
            {
                list.Remove(item);
                list.Insert(index - 1, item);
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                list.Remove(item);
                list.Insert(index + 1, item);
            }
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }





        private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
        {
            while (toCheck != null && toCheck != typeof(object))
            {
                var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
                if (generic == cur)
                {
                    return true;
                }
                toCheck = toCheck.BaseType;
            }
            return false;
        }
        private static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {
            return assembly.GetTypes()
                      .Where(t => string.Equals(t.Namespace, nameSpace, StringComparison.Ordinal))
                      .ToArray();
        } 

        protected static void LabelField(string label, Color color)
        {
            var oldColor = GUI.contentColor;
            GUI.contentColor = color;
            EditorGUILayout.LabelField(label);
            GUI.contentColor = oldColor;
        }
    }
}

