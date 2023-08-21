using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Editor.AIEditorWindow;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Base class of node drawer
    /// </summary>
    public abstract class NodeDrawerBase
    {
        public AIEditorWindow editor;
        public TreeNode node;

        private TypeReferenceDrawer TypeDrawer;
        private Vector2 listScrollView;

        [Obsolete] private ComponentReferenceDrawer CRDrawer;

        /// <summary> The behaviour tree data </summary>
        protected BehaviourTreeData tree => editor.tree;


        public NodeDrawerBase() { }
        public NodeDrawerBase(AIEditorWindow editor, TreeNode node)
        {
            this.editor = editor;
            this.node = node;
        }




        /// <summary>
        /// Call when AI editor is drawing the node
        /// </summary>
        public abstract void Draw();



        /// <summary>
        /// Draw base node info
        /// </summary>
        public void DrawNodeBaseInfo() => NodeDrawers.DrawNodeBaseInfo(tree, node);





        /// <summary>
        /// Draw a field
        /// </summary>
        /// <param name="labelName">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        protected void DrawField(string labelName, FieldInfo field, TreeNode target) => DrawField(new GUIContent(labelName), field, target);

        /// <summary>
        /// Draw a field
        /// </summary>
        /// <param name="label">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        protected void DrawField(GUIContent label, FieldInfo field, TreeNode target)
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
            if (!DrawSpecialField(label, field, target))
            {
                EditorFieldDrawers.DrawField(tree, label, field, target);
            }
        }


        private bool DrawSpecialField(GUIContent label, FieldInfo field, TreeNode target)
        {
            object value = field.GetValue(target);
            Type fieldType = field.FieldType;
            if (value is VariableBase variableFieldBase)
            {
                var possibleType = variableFieldBase.GetVariableTypes(field);
                DrawVariable(label, variableFieldBase, possibleType);
                return true;
            }
            else if (value is NodeReference rawReference)
            {
                DrawNodeReference(label, rawReference);
                return true;
            }
            else if (value is RawNodeReference reference)
            {
                DrawNodeReference(label, reference);
                return true;
            }
            else if (value is TypeReference typeReference)
            {
                DrawTypeReference(label, typeReference);
                return true;
            }
            else if (value is List<NodeReference>)
            {
                var list = (List<NodeReference>)value;
                DrawNodeList(label, list, target);
                return true;
            }
            else if (value is List<RawNodeReference>)
            {
                var list = (List<RawNodeReference>)value;
                DrawNodeList(label, list, target);
                return true;
            }
            else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var list = value as IList;
                DrawList(label, list);
                return true;
            }
            else if (CustomAIFieldDrawerAttribute.IsDrawerDefined(fieldType))
            {
                CustomAIFieldDrawerAttribute.TryInvoke(out object result, label, value, tree);
                field.SetValue(target, result);
                return true;
            }
            return false;
        }


        /// <summary>
        /// Draw a field
        /// </summary>
        /// <param name="labelName">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        protected void DrawProperty(SerializedProperty property, FieldInfo field, TreeNode target) => DrawProperty(new GUIContent(property.displayName), property, field, target);

        /// <summary>
        /// Draw a field
        /// </summary>
        /// <param name="label">label of the field</param>
        /// <param name="field">field info</param>
        /// <param name="target">target of the field</param>
        protected void DrawProperty(GUIContent label, SerializedProperty property, FieldInfo field, TreeNode target)
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


            if (!DrawSpecialField(label, field, target))
            {
                try
                {
                    EditorFieldDrawers.DrawDefaultField(EditorGUILayout.GetControlRect(), property);
                }
                catch (ExitGUIException) { throw; }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EditorGUILayout.PropertyField(property);
                }

                if (property.serializedObject.hasModifiedProperties)
                {
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            // update it
            property.serializedObject.Update();
        }




        /// <summary>
        /// Draw a type reference
        /// </summary>
        /// <remarks>
        /// This method is performance inefficient if calling this multiple times in single Node Drawer
        /// </remarks>
        /// <param name="labelName"></param>
        /// <param name="typeReference"></param>s
        /// <returns></returns>
        public void DrawTypeReference(string labelName, TypeReference typeReference) => DrawTypeReference(new GUIContent(labelName), typeReference);

        /// <summary>
        /// Draw a type reference
        /// </summary>
        /// <remarks>
        /// This method is performance inefficient if calling this multiple times in single Node Drawer
        /// </remarks>
        /// <param name="label"></param>
        /// <param name="typeReference"></param>s
        /// <returns></returns>
        public void DrawTypeReference(GUIContent label, TypeReference typeReference)
        {
            //var type = typeReference?.ReferType;
            TypeDrawer ??= new TypeReferenceDrawer(typeReference, label);
            TypeDrawer.Reset(typeReference, label);
            TypeDrawer.Draw();
            //if (type != typeReference?.ReferType)
            //{
            //    Undo.RecordObject(tree, $"Change type reference {label.text}");
            //}
        }




        /// <summary>
        /// Draw a type reference and return the drawer object
        /// </summary>
        /// <param name="label"></param>
        /// <param name="typeReference"></param>
        /// <param name="typeDrawer"></param>
        /// <returns></returns>
        internal TypeReferenceDrawer DrawTypeReference(GUIContent label, TypeReference typeReference, TypeReferenceDrawer typeDrawer = null)
        {
            typeDrawer ??= new TypeReferenceDrawer(typeReference, label);
            typeDrawer.Draw();
            return typeDrawer;
        }

        /// <summary>
        /// Draw a type reference and return the drawer object
        /// </summary>
        /// <param name="labelName"></param>
        /// <param name="typeReference"></param>
        /// <param name="typeDrawer"></param>
        /// <returns></returns>
        internal TypeReferenceDrawer DrawTypeReference(string labelName, TypeReference typeReference, TypeReferenceDrawer typeDrawer = null) => DrawTypeReference(new GUIContent(labelName), typeReference, typeDrawer);







        //public void DrawAssetReference(string labelName, AssetReferenceBase assetReferenceBase) => DrawAssetReference(new GUIContent(labelName), assetReferenceBase);
        //public void DrawAssetReference(GUIContent label, AssetReferenceBase assetReferenceBase)
        //{
        //    UnityEngine.Object currentAsset = TreeData.GetAsset(assetReferenceBase.uuid);
        //    var newAsset = EditorGUILayout.ObjectField(label, currentAsset, assetReferenceBase.GetAssetType(), false);
        //    //asset change
        //    if (newAsset != currentAsset)
        //    {
        //        TreeData.AddAsset(newAsset);
        //        assetReferenceBase.SetReference(newAsset);
        //    }
        //}



        /// <summary>
        /// Draw a node reference
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="reference">reference object</param>
        public void DrawNodeReference(string labelName, RawNodeReference reference) => DrawNodeReference(new GUIContent(labelName), reference);
        /// <summary>
        /// Draw a node reference
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="reference">reference object</param>
        public void DrawNodeReference(GUIContent label, RawNodeReference reference)
        {
            DrawNodeReference(label, reference,
            (TreeNode n) =>
            {
                reference.Node = n;
                reference.UUID = n?.uuid ?? UUID.Empty;
            });
        }
        /// <summary>
        /// Draw a node reference
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="reference">reference object</param>
        public void DrawNodeReference(string labelName, NodeReference reference) => DrawNodeReference(new GUIContent(labelName), reference);
        /// <summary>
        /// Draw a node reference
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="reference">reference object</param>
        public void DrawNodeReference(GUIContent label, NodeReference reference)
        {
            DrawNodeReference(label, reference,
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

        private void DrawNodeReference(GUIContent label, INodeReference reference, SelectNodeEvent selectNodeEvent)
        {
            reference.Node = tree.GetNode(reference.UUID);
            TreeNode referencingNode = reference.Node;
            string nodeName = referencingNode?.name ?? string.Empty;
            GUILayout.BeginHorizontal();
            label.text += nodeName;
            EditorGUILayout.LabelField(label);
            EditorGUI.indentLevel++;

            // no selection
            if (referencingNode is null)
            {
                if (GUILayout.Button("Select.."))
                {
                    if (Event.current.button == 0)
                    {
                        editor.OpenSelectionWindow(RightWindow.All, selectNodeEvent, reference.IsRawReference);
                    }
                    else if (reference is NodeReference r)
                    {
                        GenericMenu menu = new();

                        if (editor.clipboard.HasContent()) menu.AddItem(new GUIContent("Paste"), false, () => editor.clipboard.PasteUnder(editor.tree, node, r));
                        else menu.AddDisabledItem(new GUIContent("Paste"));

                        menu.ShowAsContext();
                    }
                }
            }
            // has reference
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DrawNodeReferenceModify(referencingNode, selectNodeEvent);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                NodeDrawers.DrawNodeBaseInfo(tree, referencingNode);
                EditorGUI.indentLevel = oldIndent;
            }
            EditorGUI.indentLevel--;
            GUILayout.EndHorizontal();
        }

        private void DrawNodeReferenceModify(TreeNode node, SelectNodeEvent assignmentEvent)
        {
            GUILayout.BeginHorizontal(GUILayout.Width(80));
            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.BeginVertical(GUILayout.Width(80));
            if (GUILayout.Button("Open"))
            {
                Debug.Log("Open");
                editor.SelectedNode = node;
            }
            else if (GUILayout.Button("Replace"))
            {
                editor.OpenSelectionWindow(RightWindow.All, (n) => { node.parent = NodeReference.Empty; assignmentEvent(n); });
            }
            else if (GUILayout.Button("Delete"))
            {
                assignmentEvent(null);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }


        /// <summary>
        /// Draw variable field, same as <seealso cref="VariableFieldDrawers.DrawVariable(string, VariableBase, BehaviourTreeData, VariableType[])"/>
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public void DrawVariable(string labelName, VariableBase variable, VariableType[] possibleTypes = null) => VariableFieldDrawers.DrawVariable(labelName, variable, tree, possibleTypes);
        /// <summary>
        /// Draw variable field, same as <seealso cref="VariableFieldDrawers.DrawVariable(GUIContent, VariableBase, BehaviourTreeData, VariableType[])"/>
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public void DrawVariable(GUIContent label, VariableBase variable, VariableType[] possibleTypes = null) => VariableFieldDrawers.DrawVariable(label, variable, tree, possibleTypes);




        [Obsolete("Do not use uuid list")]
        protected void DrawNodeList(string listName, List<UUID> list, TreeNode node)
        {
            EditorGUILayout.LabelField(listName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                UUID item = list[i];
                var childNode = tree.GetNode(item);
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
                    NodeDrawers.DrawNodeBaseInfo(tree, childNode);
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
                editor.OpenSelectionWindow(RightWindow.All, (n) =>
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

        protected void DrawNodeList(string labelName, List<RawNodeReference> list, TreeNode node) => DrawNodeList(new GUIContent(labelName), list, node);
        protected void DrawNodeList(GUIContent label, List<RawNodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                RawNodeReference item = list[i];
                var childNode = tree.GetNode(item.UUID);


                Color color = i % 2 == 0 ? Color.white * (80 / 255f) : Color.white * (64 / 255f);
                var colorStyle = SetRegionColor(color, out var baseColor);
                GUILayout.BeginHorizontal(colorStyle);
                GUI.backgroundColor = baseColor;


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
                    NodeDrawers.DrawNodeBaseInfo(tree, childNode);
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
            //GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                editor.OpenSelectionWindow(RightWindow.All, (n) =>
                {
                    list.Add(n.ToRawReference());
                }, true);
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            //GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        protected void DrawNodeList(string labelName, List<NodeReference> list, TreeNode node) => DrawNodeList(new GUIContent(labelName), list, node);
        protected void DrawNodeList(GUIContent label, List<NodeReference> list, TreeNode node)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            listScrollView = GUILayout.BeginScrollView(listScrollView);
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];
                var childNode = tree.GetNode(item.UUID);

                Color color = i % 2 == 0 ? Color.white * (80 / 255f) : Color.white * (64 / 255f);
                var colorStyle = SetRegionColor(color, out var baseColor);
                GUILayout.BeginHorizontal(colorStyle);
                GUI.backgroundColor = baseColor;

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
                    NodeDrawers.DrawNodeBaseInfo(tree, childNode);
                    EditorGUI.indentLevel--;
                    GUILayout.EndVertical();
                }
                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
            GUILayout.EndScrollView();
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            //GUILayout.BeginVertical();
            if (GUILayout.Button("Add"))
            {
                if (Event.current.button == 0)
                {
                    OpenEditorSelectWindow(list, node);
                }
                else
                {
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Add"), false, () => OpenEditorSelectWindow(list, node));
                    if (node is IListFlow lf)
                    {
                        menu.AddItem(new GUIContent("Paste Append"), false, () => editor.clipboard.PasteAppend(editor.tree, lf));
                    }
                    menu.ShowAsContext();
                }
            }
            if (list.Count != 0) if (GUILayout.Button("Remove"))
                {
                    list.RemoveAt(list.Count - 1);
                }
            //GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            void OpenEditorSelectWindow(List<NodeReference> list, TreeNode node)
            {
                editor.OpenSelectionWindow(RightWindow.All, (n) =>
                {
                    list.Add(n);
                    n.parent = node;
                    if (editor.reachableNodes.Contains(node)) { editor.reachableNodes.Add(n); }
                });
            }
        }




        protected void DrawNodeListItemCommonModify<T>(List<T> list, int index)
        {
            GUILayout.BeginHorizontal(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));
            GUILayout.Space(EditorGUI.indentLevel * 16 + 4);
            if (GUILayout.Button("x", GUILayout.MaxWidth(18)))
            {
                DrawNodeListItemCommonModify_Remove(list, index);
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.BeginVertical(GUILayout.MaxWidth(60), GUILayout.MinWidth(60));

            //GUILayout.Space((EditorGUI.indentLevel - 1) * 16);
            if (GUILayout.Button("Open", GUILayout.MaxWidth(60)))
            {
                T t = list[index];
                TreeNode nodeElement = GetTreeNodeFromElement(t);
                editor.SelectedNode = nodeElement;
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }

            if (list[index] is INodeReference && GUILayout.Button("Replace"))
            {
                DrawNodeListItemCommonModify_Replace(list, index);
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }

            var currentStatus = GUI.enabled;
            if (index == 0) GUI.enabled = false;
            if (GUILayout.Button("Up"))
            {
                ListItem_Up(list, index);
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                ListItem_Down(list, index);
                GUILayout.EndVertical(); GUILayout.EndHorizontal(); return;
            }
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawNodeListItemCommonModify_Remove<T>(List<T> list, int index)
        {
            if (Event.current.button == 0)
            {
                if (editor.editorSetting.debugMode) Debug.Log("Delete");
                int opt = EditorUtility.DisplayDialogComplex("Delete list element", "Delete removed node from the tree?", "Delete", "Cancel", "Remove Only");
                switch (opt)
                {
                    case 0:
                        TreeNode childNode = RemoveFromList(list, index);
                        editor.TryDeleteNode(childNode);
                        break;
                    case 1:
                        break;
                    case 2:
                        RemoveFromList(list, index);
                        break;
                }
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Remove element"), false, () => RemoveFromList(list, index));
                menu.AddItem(new GUIContent("Remove and delete"), false, () => { var childNode = RemoveFromList(list, index); editor.TryDeleteNode(childNode); });
                menu.ShowAsContext();
            }
        }

        private void DrawNodeListItemCommonModify_Replace<T>(List<T> list, int index)
        {
            T t = list[index];
            TreeNode nodeElement = GetTreeNodeFromElement(t);
            var listClone = list.ShallowCloneToList();
            editor.OpenSelectionWindow(RightWindow.All,
            (n) =>
            {
                // replacing same node
                if (n == nodeElement)
                {
                    list.Insert(index, t);
                    EditorUtility.DisplayDialog("Replacing node error",
                        $"Cannot replace node {nodeElement.name} because selected node is same as the old one",
                        "ok");
                    return;
                }
                // switch
                var oldT = listClone.FirstOrDefault(e => GetTreeNodeFromElement(e) == n);
                if (oldT != null)
                {
                    int targetIndex = listClone.IndexOf(oldT);
                    list.Insert(targetIndex, oldT);
                    (list[targetIndex], list[index]) = (list[index], list[targetIndex]);
                    return;
                }
                // new
                node.parent = NodeReference.Empty;
                Debug.Log("Set Connection");
                if (n is not null)
                {
                    ((INodeReference)t).UUID = n.uuid;
                    n.parent = node;
                }
                else
                {
                    ((INodeReference)t).UUID = UUID.Empty;
                }
            });
        }

        /// <summary>
        /// Remove helper of node list item modifier
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private TreeNode RemoveFromList<T>(List<T> list, int index)
        {
            T element = list[index];
            TreeNode childNode = GetTreeNodeFromElement(element);
            list.RemoveAt(index);
            childNode.parent = NodeReference.Empty;

            return childNode;
        }

        private void ListItem_Down(IList list, int index)
        {
            if (Event.current.button == 0)
            {
                (list[index], list[index + 1]) = (list[index + 1], list[index]);
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Down 1"), false, () => (list[index], list[index + 1]) = (list[index + 1], list[index]));
                menu.AddItem(new GUIContent("To Last"), false, () => { var item = list[index]; list.RemoveAt(index); list.Add(item); });
                menu.ShowAsContext();
            }
        }

        private void ListItem_Up(IList list, int index)
        {
            if (Event.current.button == 0)
            {
                (list[index], list[index - 1]) = (list[index - 1], list[index]);
            }
            else
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Up 1"), false, () => (list[index], list[index - 1]) = (list[index - 1], list[index]));
                menu.AddItem(new GUIContent("To First"), false, () => { var item = list[index]; list.RemoveAt(index); list.Insert(0, item); });
                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Convert T to tree node if possible
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element"></param>
        /// <returns></returns>
        private TreeNode GetTreeNodeFromElement<T>(T element)
        {
            TreeNode childNode;
            switch (element)
            {
                case UUID uuid:
                    childNode = tree.GetNode(uuid);
                    break;
                case TreeNode n:
                    childNode = n;
                    break;
                case NodeReference nr:
                    childNode = tree.GetNode(nr);
                    break;
                case RawNodeReference rnr:
                    childNode = tree.GetNode(rnr);
                    break;
                case Probability.EventWeight w:
                    childNode = tree.GetNode(w.reference);
                    break;
                default:
                    Debug.Log("Cannot determine list type " + element.GetType().Name);
                    childNode = null;
                    break;
            }

            return childNode;
        }

        //private T GetElement<T>(TreeNode n)
        //{
        //    Type type = typeof(T);
        //    if (type == typeof(NodeReference))
        //    {
        //        return (T)(object)(NodeReference)n;
        //    }
        //    if (type == typeof(RawNodeReference))
        //    {
        //        return (T)(object)(new RawNodeReference() { UUID = n.uuid });
        //    }
        //    if (type == typeof(UUID))
        //    {
        //        return (T)(object)n.uuid;
        //    }
        //    if (type == typeof(TreeNode))
        //    {
        //        return (T)(object)n;
        //    }
        //}

        protected void DrawList(string labelName, IList list) => DrawList(new GUIContent(labelName), list);
        protected void DrawList(GUIContent label, IList list)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                GUILayout.BeginHorizontal();
                DrawListItemCommonModify(list, i);
                if (list.Count == 0)
                {
                    GUILayout.EndHorizontal();
                    break;
                }

                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                list[i] = EditorFieldDrawers.DrawField(i.ToString(), ref item, objectToUndo: tree);

                EditorGUI.indentLevel = oldIndent;
                GUILayout.EndHorizontal();
                //GUILayout.Space(10);
            }
            EditorGUI.indentLevel--;

            GUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 16);
            //GUILayout.BeginVertical();
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
            //GUILayout.EndVertical();
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Up"))
            {
                ListItem_Up(list, index);
                //list.Remove(item);
                //list.Insert(index - 1, item);
            }
            GUI.enabled = currentStatus;
            if (index == list.Count - 1) GUI.enabled = false;
            if (GUILayout.Button("Down"))
            {
                ListItem_Down(list, index);
                //list.Remove(item);
                //list.Insert(index + 1, item);
            }
            GUILayout.EndHorizontal();
            GUI.enabled = currentStatus;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }




        /// <summary>
        /// Create a right click menu for last GUI Rect
        /// </summary>
        /// <param name="menu"></param>
        protected bool RightClickMenu(GenericMenu menu) => EditorFieldDrawers.RightClickMenu(menu);

        /// <summary>
        /// Create a right click menu for Given Rect
        /// </summary>
        /// <param name="menu"></param>
        protected bool RightClickMenu(GenericMenu menu, Rect rect) => EditorFieldDrawers.RightClickMenu(menu, rect);





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


        [Obsolete]
        public void DrawComponent(string labelName, ComponentReference componentReference)
        {
            CRDrawer ??= new ComponentReferenceDrawer(componentReference, labelName);
            CRDrawer.Reset(componentReference, labelName);
            CRDrawer.Draw();
        }

        protected GUIStyle SetRegionColor(Color color, out Color baseColor)
        {
            GUIStyle colorStyle = new();
            colorStyle.normal.background = Texture2D.whiteTexture;

            baseColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            return colorStyle;
        }

        /// <summary>
        /// Show warning when reference is null
        /// </summary>
        /// <param name="reference"></param>
        protected void NodeMustNotBeNull(NodeReference reference, string fieldName)
        {
            if (!reference.HasEditorReference)
            {
                EditorGUILayout.HelpBox($"{node.GetType()} \"{node.name}\" does not have a valid {fieldName} node, this will cause error during runtime!", MessageType.Warning);
                return;
            }
        }

        [Obsolete("Calling method with wrong argument")]
        protected void NodeMustNotBeNull(TreeNode reference, string fieldName)
        {
            throw new NotImplementedException();
        }

    }
}

