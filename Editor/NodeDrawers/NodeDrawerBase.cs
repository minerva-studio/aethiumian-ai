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
    /// <summary>
    /// Base class of node drawer
    /// </summary>
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
        private TypeReferenceDrawer TypeDrawer;

        /// <summary> The behaviour tree data </summary>
        protected BehaviourTreeData Tree => editor.tree;


        public NodeDrawerBase() { }
        public NodeDrawerBase(AIEditor editor, TreeNode node)
        {
            this.editor = editor;
            this.node = node;
        }

        public abstract void Draw();




        public void DrawNodeBaseInfo()
        {
            NodeDrawers.DrawNodeBaseInfo(node);
            if (node is Service service) DrawServiceBase(service);
        }

        protected void DrawServiceBase(Service service)
        {
            intervalMode = (IntervalMode)EditorGUILayout.EnumPopup("Interval Mode", intervalMode);
            switch (intervalMode)
            {
                case IntervalMode.frame:
                    service.interval = EditorGUILayout.IntField("Interval (frames)", service.interval);
                    if (service.interval < 0) service.interval = 0;
                    time = service.interval / 60f;
                    break;
                case IntervalMode.realTime:
                    time = EditorGUILayout.FloatField("Time (seconds)", time);
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

            object value = field.GetValue(target);
            //special case
            if (value is VariableBase variableFieldBase)
            {
                var possibleType = variableFieldBase.GetVariableTypes(field);
                DrawVariable(labelName, variableFieldBase, possibleType);
            }
            else if (value is NodeReference rawReference)
            {
                DrawNodeReference(labelName, rawReference);
            }
            else if (value is RawNodeReference reference)
            {
                DrawNodeReference(labelName, reference);
            }
            else if (value is TypeReference typeReference)
            {
                DrawType(labelName, typeReference);
            }
            else if (value is ComponentReference componentReference)
            {
                DrawComponent(labelName, componentReference);
            }
            else if (value is AssetReferenceBase assetReference)
            {
                DrawAssetReference(labelName, assetReference);
            }
            else if (value is List<NodeReference>)
            {
                var list = (List<NodeReference>)value;
                DrawNodeList(labelName, list, target);
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = field.GetValue(target) as IList;
                DrawList(labelName, list);
            }
            else EditorFieldDrawers.DrawField(labelName, field, target);
        }

        public void DrawType(string labelName, TypeReference typeReference)
        {
            TypeDrawer ??= new TypeReferenceDrawer(typeReference, labelName);
            TypeDrawer.Reset(typeReference, labelName);
            TypeDrawer.Draw();
        }

        internal TypeReferenceDrawer DrawType(string labelName, TypeReference typeReference, TypeReferenceDrawer typeDrawer = null)
        {
            typeDrawer ??= new TypeReferenceDrawer(typeReference, labelName);
            typeDrawer.Draw();
            return typeDrawer;
        }

        [Obsolete]
        public void DrawComponent(string labelName, ComponentReference componentReference)
        {
            CRDrawer ??= new ComponentReferenceDrawer(componentReference, labelName);
            CRDrawer.Reset(componentReference, labelName);
            CRDrawer.Draw();
        } 

        public void DrawAssetReference(string labelName, AssetReferenceBase assetReferenceBase)
        {
            UnityEngine.Object currentAsset = Tree.GetAsset(assetReferenceBase.uuid);
            var newAsset = EditorGUILayout.ObjectField(labelName, currentAsset, assetReferenceBase.GetAssetType(), false);
            //asset change
            if (newAsset != currentAsset)
            {
                Tree.AddAsset(newAsset);
                assetReferenceBase.SetReference(newAsset);
            }
        }

        public void DrawNodeReference(string labelName, RawNodeReference reference)
        {
            DrawINodeReference(labelName, reference,
            (TreeNode n) =>
            {
                reference.Node = n;
                if (n is not null)
                {
                    reference.UUID = n.uuid;
                }
                else
                {
                    reference.UUID = UUID.Empty;
                }
            });
        }

        public void DrawNodeReference(string labelName, NodeReference reference)
        {
            DrawINodeReference(labelName, reference,
            (TreeNode n) =>
            {
                reference.Node = n;
                if (n is not null)
                {
                    reference.UUID = n.uuid;
                    n.parent = node;
                }
                else
                {
                    reference.UUID = UUID.Empty;
                }
            });
        }
        public void DrawINodeReference(string labelName, INodeReference reference, SelectNodeEvent selectNodeEvent)
        {
            reference.Node = Tree.GetNode(reference.UUID);
            TreeNode referencingNode = reference.Node;
            string nodeName = referencingNode?.name ?? string.Empty;
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(labelName + ": " + nodeName);
            EditorGUI.indentLevel++;
            if (referencingNode is null)
            {
                if (GUILayout.Button("Select.."))
                {
                    editor.OpenSelectionWindow(RightWindow.all, selectNodeEvent);
                    editor.rawReferenceSelect = reference.IsRawReference;
                }
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DrawNodeListItemCommonModify(referencingNode, selectNodeEvent);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                NodeDrawers.DrawNodeBaseInfo(referencingNode);
                EditorGUI.indentLevel = oldIndent;
            }
            EditorGUI.indentLevel--;
            GUILayout.EndHorizontal();
        }

        public void DrawVariable(string labelName, VariableBase variable, VariableType[] possibleTypes = null) => VariableDrawers.DrawVariable(labelName, Tree, variable, possibleTypes);


        [Obsolete]
        protected void DrawNodeList(string listName, List<UUID> list, TreeNode node)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                UUID item = list[i];
                var childNode = Tree.GetNode(item);
                GUILayout.BeginHorizontal();
                DrawNodeListItemCommonModify(list, i);
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
                RawNodeReference item = list[i];
                var childNode = Tree.GetNode(item.UUID);
                GUILayout.BeginHorizontal();
                DrawNodeListItemCommonModify(list, i);
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
                    list.Add(n.ToRawReference());
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
                var childNode = Tree.GetNode(item.UUID);
                GUILayout.BeginHorizontal();
                DrawNodeListItemCommonModify(list, i);
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

        protected void DrawNodeListItemCommonModify<T>(List<T> list, int index)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                Debug.Log("Delete");
                T t = list[index];
                list.RemoveAt(index);
                switch (t)
                {
                    case UUID uuid:
                        Tree.GetNode(uuid).parent = NodeReference.Empty;
                        break;
                    case TreeNode n:
                        n.parent = NodeReference.Empty;
                        break;
                    case NodeReference nr:
                        nr.UUID = UUID.Empty;
                        nr.Node = null;
                        break;
                    case RawNodeReference rnr:
                        rnr.UUID = UUID.Empty;
                        rnr.Node = null;
                        break;
                    case Probability.EventWeight w:
                        Tree.GetNode(w.reference).parent = NodeReference.Empty;
                        break;
                    default:
                        Debug.Log("Cannot determine list type " + t.GetType().Name);
                        break;
                }
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(60)))
            {
                T t = list[index];
                switch (t)
                {
                    case UUID uuid:
                        editor.SelectedNode = Tree.GetNode(uuid);
                        break;
                    case TreeNode n:
                        editor.SelectedNode = n;
                        break;
                    case NodeReference nr:
                        editor.SelectedNode = Tree.GetNode(nr.UUID);
                        break;
                    case RawNodeReference rnr:
                        editor.SelectedNode = Tree.GetNode(rnr.UUID);
                        break;
                    case Probability.EventWeight w:
                        editor.SelectedNode = Tree.GetNode(w.reference);
                        break;
                    default:
                        Debug.Log("Cannot open in AI Editor" + t.GetType().Name);
                        break;
                }
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

