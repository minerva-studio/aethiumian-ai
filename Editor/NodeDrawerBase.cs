using Amlos.AI;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Amlos.Editor.AIEditor;

namespace Amlos.Editor
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




        public void DrawNodeBaseInfo() => DrawNodeBaseInfo(node);
        protected void DrawNodeBaseInfo(TreeNode treeNode)
        {
            GUILayout.BeginVertical();
            var currentStatus = GUI.enabled;
            GUI.enabled = false;
            var script = UnityEngine.Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(n => n.GetClass() == treeNode.GetType());
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            GUI.enabled = currentStatus;

            treeNode.name = EditorGUILayout.TextField("Name", treeNode.name);
            EditorGUILayout.LabelField("UUID", treeNode.uuid);

            GUILayout.EndVertical();
        }


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
            service.randomDeviation = DrawRangeField("Deviation", service.randomDeviation);
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
            if (fieldType.IsSubclassOf(typeof(VariableFieldBase)))
            {
                VariableFieldBase variableFieldBase = (VariableFieldBase)field.GetValue(target);
                var possibleType = GetVariableTypes(field, variableFieldBase);
                DrawVariable(labelName, variableFieldBase, possibleType);
            }
            else if (fieldType == (typeof(NodeReference)))
            {
                DrawNodeSelection(labelName, (NodeReference)field.GetValue(target));
            }
            else if (fieldType == (typeof(ComponentReference)))
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
            else DrawBasicField(target, field, labelName);
        }

        protected void DrawBasicField(object target, FieldInfo field, string labelName)
        {
            if (field is null)
            {
                EditorGUILayout.LabelField(labelName, "Field not instantiated");
                return;
            }
            object value = DrawBasicField(field.GetValue(target), labelName);
            if (value is not null) field.SetValue(target, value);
        }

        protected object DrawBasicField(object value, string labelName)
        {
            if (value is string s)
            {
                return EditorGUILayout.TextField(labelName, s);
            }
            else if (value is int i)
            {
                return EditorGUILayout.IntField(labelName, i);
            }
            else if (value is float f)
            {
                return EditorGUILayout.FloatField(labelName, f);
            }
            else if (value is double d)
            {
                return EditorGUILayout.DoubleField(labelName, d);
            }
            else if (value is bool b)
            {
                return EditorGUILayout.Toggle(labelName, b);
            }
            else if (value is Vector2 v2)
            {
                return EditorGUILayout.Vector2Field(labelName, v2);
            }
            else if (value is Vector2Int v2i)
            {
                return EditorGUILayout.Vector2IntField(labelName, v2i);
            }
            else if (value is Vector3 v3)
            {
                return EditorGUILayout.Vector3Field(labelName, v3);
            }
            else if (value is Vector3Int v3i)
            {
                return EditorGUILayout.Vector3IntField(labelName, v3i);
            }
            else if (value is Enum e)
            {
                if (Attribute.GetCustomAttribute(value.GetType(), typeof(FlagsAttribute)) != null)
                {
                    return EditorGUILayout.EnumFlagsField(labelName, e);
                }
                else return EditorGUILayout.EnumPopup(labelName, e);
            }
            else if (value is UnityEngine.Object uo)
            {
                return EditorGUILayout.ObjectField(labelName, uo, value.GetType(), false);
            }
            else if (value is Minerva.Module.RangeInt r)
            {
                return DrawRangeField(labelName, r);
            }
            else EditorGUILayout.LabelField("Do not support drawing type " + value?.GetType().Name ?? "");
            return value;
        }



        protected Minerva.Module.RangeInt DrawRangeField(string labelName, Minerva.Module.RangeInt value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(labelName, GUILayout.MaxWidth(150));
            EditorGUILayout.LabelField("Min", GUILayout.MaxWidth(30));
            int min = EditorGUILayout.IntField(value.min);
            EditorGUILayout.LabelField("Max", GUILayout.MaxWidth(30));
            int max = EditorGUILayout.IntField(value.max);
            EditorGUILayout.EndHorizontal();
            Minerva.Module.RangeInt ret = new Minerva.Module.RangeInt(min, max);
            return ret;
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
                tree.SetAsset(newAsset);
                assetReferenceBase.SetReference(newAsset);
            }
        }

        protected void DrawVariable(string labelName, VariableFieldBase variable, VariableType[] possibleTypes = null)
        {
            possibleTypes ??= (VariableType[])Enum.GetValues(typeof(VariableType));

            if (variable.GetType().IsGenericType && variable.GetType().GetGenericTypeDefinition() == typeof(VariableReference<>))
            {
                DrawVariableReference(labelName, variable, possibleTypes);
            }
            else if (variable.GetType() == typeof(VariableReference))
            {
                DrawVariableReference(labelName, variable, possibleTypes);
            }
            else DrawVariableField(labelName, variable, possibleTypes);
        }

        private void DrawVariableReference(string labelName, VariableFieldBase variable, VariableType[] possibleTypes) => DrawVariableSelection(labelName, variable, possibleTypes);

        private void DrawVariableField(string labelName, VariableFieldBase variable, VariableType[] possibleTypes)
        {
            if (variable.IsConstant) DrawVariableConstant(labelName, variable, possibleTypes);
            else DrawVariableSelection(labelName, variable, possibleTypes, true);
        }

        private void DrawVariableConstant(string labelName, VariableFieldBase variable, VariableType[] possibleTypes)
        {
            VariableField f;
            FieldInfo newField;
            switch (variable.Type)
            {
                case VariableType.String:
                    newField = variable.GetType().GetField(nameof(f.stringValue));
                    break;
                case VariableType.Int:
                    newField = variable.GetType().GetField(nameof(f.intValue));
                    break;
                case VariableType.Float:
                    newField = variable.GetType().GetField(nameof(f.floatValue));
                    break;
                case VariableType.Bool:
                    newField = variable.GetType().GetField(nameof(f.boolValue));
                    break;
                case VariableType.Vector2:
                    newField = variable.GetType().GetField(nameof(f.vector2Value));
                    break;
                case VariableType.Vector3:
                    newField = variable.GetType().GetField(nameof(f.vector3Value));
                    break;
                default:
                    newField = null;
                    break;
            }
            GUILayout.BeginHorizontal();
            DrawBasicField(variable, newField, labelName);
            if (variable is VariableField vf && variable is not Parameter && vf.IsGeneric && vf.IsConstant)
            {
                variable.Type = (VariableType)EditorGUILayout.EnumPopup(GUIContent.none, variable.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(80));
            }
            if (tree.variables.Count > 0 && GUILayout.Button("Use Variable", GUILayout.MaxWidth(100)))
            {
                variable.SetReference(tree.variables[0]);
            }
            GUILayout.EndHorizontal();
            bool CanDisplay(Enum val)
            {
                return Array.IndexOf(possibleTypes, val) != -1;
            }
        }

        private void DrawVariableSelection(string labelName, VariableFieldBase variable, VariableType[] possibleTypes, bool allowConvertToConstant = false)
        {
            GUILayout.BeginHorizontal();

            string[] list;
            IEnumerable<VariableData> vars =
                variable.IsGeneric
                ? tree.variables.Where(v => Array.IndexOf(possibleTypes, v.type) != -1)
                : tree.variables.Where(v => v.type == variable.Type && Array.IndexOf(possibleTypes, v.type) != -1);
            ;
            list = vars.Select(v => v.name).Append("Create New...").ToArray();

            if (list.Length < 2)
            {
                EditorGUILayout.LabelField(labelName, "No valid variable found");
                if (GUILayout.Button("Create New", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
            }
            else
            {
                string variableName = tree.GetVariable(variable.UUID)?.name ?? "";
                if (Array.IndexOf(list, variableName) == -1)
                {
                    variableName = list[0];
                }
                int selectedIndex = Array.IndexOf(list, variableName);
                if (selectedIndex < 0)
                {
                    if (!variable.HasReference)
                    {
                        EditorGUILayout.LabelField(labelName, $"No Variable");
                        if (GUILayout.Button("Create", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type));
                    }
                    else
                    {
                        EditorGUILayout.LabelField(labelName, $"Variable {variableName} not found");
                        if (GUILayout.Button("Recreate", GUILayout.MaxWidth(80))) variable.SetReference(tree.CreateNewVariable(variable.Type, variableName));
                        if (GUILayout.Button("Clear", GUILayout.MaxWidth(80))) variable.SetReference(null);
                    }
                }
                else
                {
                    int currentIndex = EditorGUILayout.Popup(labelName, selectedIndex, list, GUILayout.MinWidth(400));
                    if (currentIndex < 0) { currentIndex = 0; }
                    //using existing var
                    if (currentIndex != list.Length - 1)
                    {
                        string varName = list[currentIndex];
                        var a = tree.GetVariable(varName);
                        variable.SetReference(a);
                    }
                    //Create new var
                    else
                    {
                        tree.CreateNewVariable(variable.Constant);
                    }
                }
            }


            if (variable.IsGeneric && variable.IsConstant)
            {
                variable.Type = (VariableType)EditorGUILayout.EnumPopup(GUIContent.none, variable.Type, CanDisplay, false, EditorStyles.popup, GUILayout.MaxWidth(80));
            }

            if (allowConvertToConstant)
            {
                if (GUILayout.Button("Set Constant", GUILayout.MaxWidth(100)))
                {
                    variable.SetReference(null);
                }
            }
            else
            {
                EditorGUILayout.LabelField("         ", GUILayout.MaxWidth(100));
            }
            GUILayout.EndHorizontal();


            bool CanDisplay(Enum val)
            {
                return Array.IndexOf(possibleTypes, val) != -1;
            }
        }

        private VariableType[] GetVariableTypes(MemberInfo fieldBaseMemberInfo, VariableFieldBase fieldBase)
        {
            //non generic case
            if (!(fieldBase is VariableField || fieldBase is VariableReference))
            {
                return new VariableType[] { fieldBase.Type };
            }

            //generic case 
            var possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(TypeLimitAttribute)) is TypeLimitAttribute varLimit
                ? varLimit.VariableTypes
                : (VariableType[])Enum.GetValues(typeof(VariableType));

            possible = Attribute.GetCustomAttribute(fieldBaseMemberInfo, typeof(TypeExcludeAttribute)) is TypeExcludeAttribute varExclude
                ? possible.Except(varExclude.VariableTypes).ToArray()
                : possible;

            return possible;
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
                    EditorGUILayout.LabelField(GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    DrawNodeBaseInfo(childNode);
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
                editor.OpenSelectionWindow(RightWindow.nodeType, (n) =>
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
                    EditorGUILayout.LabelField(GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    DrawNodeBaseInfo(childNode);
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
                editor.OpenSelectionWindow(RightWindow.nodeType, (n) =>
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
                    EditorGUILayout.LabelField(GetEditorName(childNode));
                    EditorGUI.indentLevel++;
                    DrawNodeBaseInfo(childNode);
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
                editor.OpenSelectionWindow(RightWindow.nodeType, (n) =>
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

                list[i] = DrawBasicField(item, i.ToString());

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
                    editor.OpenSelectionWindow(RightWindow.nodeType, SelectEvent);
            }
            else
            {
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                DrawItemCommonModify(referencingNode, SelectEvent);
                var oldIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 1;
                DrawNodeBaseInfo(referencingNode);
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
        //[Obsolete]
        //protected void DrawNodeSelection(string label, TreeNode node, SelectNodeEvent selectEvent)
        //{
        //    string nodeName = node?.name ?? string.Empty;
        //    GUILayout.BeginHorizontal();
        //    EditorGUILayout.LabelField(label + ": " + nodeName);
        //    if (node is null)
        //    {
        //        if (GUILayout.Button("Select.."))
        //            editor.OpenSelectionWindow(RightWindow.nodeType, selectEvent);
        //        GUILayout.EndHorizontal();
        //        return;
        //    }
        //    GUILayout.EndHorizontal();
        //    GUILayout.BeginHorizontal();
        //    DrawItemCommonModify(node, selectEvent);
        //    var oldIndent = EditorGUI.indentLevel;
        //    EditorGUI.indentLevel = 1;
        //    DrawNodeBaseInfo(node);
        //    EditorGUI.indentLevel = oldIndent;
        //    GUILayout.EndHorizontal();
        //}

        protected void DrawItemCommonModify(TreeNode node, SelectNodeEvent assignmentEvent)
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
                editor.OpenSelectionWindow(RightWindow.nodeType, (n) => { node.parent = NodeReference.Empty; assignmentEvent(n); });
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
        public static string GetEditorName(TreeNode node)
        {
            return node.name + $" ({node.GetType().Name.ToTitleCase()})";
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

