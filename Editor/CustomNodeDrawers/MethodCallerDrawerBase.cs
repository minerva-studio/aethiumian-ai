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
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    public abstract class MethodCallerDrawerBase : NodeDrawerBase
    {
        /// <summary>
        /// Binding flags for instance method
        /// </summary>
        protected const BindingFlags INSTANCE_MEMBER = BindingFlags.Public | BindingFlags.Instance;
        protected const BindingFlags STATIC_MEMBER = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        protected bool showParentMethod;
        protected int selected;
        protected Type refType;
        protected MethodInfo[] methods;
        internal TypeReferenceDrawer typeReferenceDrawer;
        protected Vector2 fieldListScroll;

        protected virtual BindingFlags Binding => INSTANCE_MEMBER;

        /// <summary>
        /// Check whether method is valid
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        protected virtual bool IsValidMethod(MethodInfo method)
        {
            if (method.IsGenericMethod) return false;
            if (method.IsGenericMethodDefinition) return false;
            if (method.ContainsGenericParameters) return false;
            if (Attribute.IsDefined(method, typeof(ObsoleteAttribute))) return false;
            if (method.IsSpecialName) return false;
            ParameterInfo[] parameterInfos = method.GetParameters();
            if (parameterInfos.Length == 0) return true;


            for (int i = 0; i < parameterInfos.Length; i++)
            {
                ParameterInfo item = parameterInfos[i];
                VariableType variableType = VariableUtility.GetVariableType(item.ParameterType);
                if (variableType == VariableType.Invalid) return false;
                if (variableType == VariableType.Node && (i != 0 || item.ParameterType != typeof(NodeProgress)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Draw component selection
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        protected bool DrawComponent(IComponentCaller caller)
        {
            EditorGUILayout.LabelField("Component Data", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            caller.GetComponent = EditorGUILayout.Toggle("Get Component", caller.GetComponent);
            if (!caller.GetComponent)
            {
                var oldReferVar = caller.Component.UUID;
                DrawVariable("Component", caller.Component, new VariableType[] { VariableType.UnityObject, VariableType.Generic });
                VariableData variableData = tree.GetVariable(caller.Component.UUID);
                // if there are changes in var selection
                if (variableData != null && oldReferVar != variableData.UUID) caller.TypeReference.SetReferType(variableData.ObjectType);

                if (!caller.Component.HasEditorReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No Component Assigned");
                    EditorGUI.indentLevel--;
                    return false;
                }
            }
            EditorGUI.indentLevel--;
            return true;
        }

        /// <summary>
        /// Draw component selection
        /// </summary>
        /// <param name="caller"></param>
        /// <returns></returns>
        protected bool DrawObject(IObjectCaller caller, out Type objectType)
        {
            objectType = null;

            EditorGUILayout.LabelField("Object Data", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            DrawVariable("Object", caller.Object, new VariableType[] { VariableType.UnityObject, VariableType.Generic });
            VariableData variableData = tree.GetVariable(caller.Object.UUID);
            if (variableData != null
                && caller.TypeReference.HasReferType
                && variableData.ObjectType != null
                && !caller.TypeReference.IsSubclassOf(variableData.ObjectType))
            {
                caller.TypeReference.SetReferType(variableData.ObjectType);
            }

            if (!caller.Object.HasEditorReference)
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("No Object Assigned");
                EditorGUI.indentLevel--;
                return false;
            }

            DrawTypeReference("Type", caller.TypeReference);

            GUILayout.BeginHorizontal();
            GUILayout.Space(30);
            if (GUILayout.Button("Use variable default type"))
            {
                caller.TypeReference.SetReferType(variableData.ObjectType);
            }
            GUILayout.EndHorizontal();

            objectType = caller.TypeReference;
            if (objectType == null)
            {
                GUILayout.Space(20);
                EditorGUILayout.LabelField("Type is not valid");
                return false;
            }
            EditorGUI.indentLevel--;
            return true;
        }

        /// <summary>
        /// Generic Method caller: Draw Refer type and get method
        /// </summary>
        /// <param name="caller"></param> 
        /// <returns></returns>
        protected bool DrawReferType(IGenericMethodCaller caller, BindingFlags bindings)
        {
            EditorGUI.indentLevel++;
            typeReferenceDrawer = DrawTypeReference("Type", caller.TypeReference, typeReferenceDrawer);

            GenericMenu menu = new();
            if (tree.targetScript)
                menu.AddItem(new GUIContent("Use Target Script Type"), false, () => caller.TypeReference.SetReferType(tree.targetScript.GetClass()));
            if (caller is IComponentCaller ccer && !ccer.GetComponent)
                menu.AddItem(new GUIContent("Use Variable Type"), false, () => caller.TypeReference.SetReferType(tree.GetVariableType(ccer.Component.UUID)));
            RightClickMenu(menu);


            if (caller.TypeReference.ReferType == null)
            {
                EditorGUILayout.LabelField("Cannot load type");
                EditorGUI.indentLevel--;
                return false;
            }
            if (caller.TypeReference.ReferType != refType)
            {
                methods = GetMethods(caller.TypeReference.ReferType, bindings);
                if (!showParentMethod)
                {
                    var selfDeclared = methods.Where(m => m.DeclaringType == caller.TypeReference.ReferType).ToArray();
                    // array does exist method name
                    if (Array.IndexOf(selfDeclared, caller.MethodName) != -1)
                    {
                        methods = selfDeclared.ToArray();
                    }
                    else
                    {
                        showParentMethod = true;
                    }
                }

                refType = caller.TypeReference.ReferType;
            }
            EditorGUI.indentLevel--;
            return true;
        }

        /// <summary>
        /// Draw action data
        /// </summary>
        /// <param name="caller"></param>
        protected void DrawActionData(ObjectActionBase caller)
        {
            EditorGUILayout.LabelField("Action Execution", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            caller.count ??= new VariableField<int>();
            caller.duration ??= new VariableField<float>();
            caller.actionCallTime =
                (ObjectActionBase.ActionCallTime)
                EditorGUILayout.EnumPopup("Action Call Time", caller.actionCallTime);
            if (caller.actionCallTime == ObjectActionBase.ActionCallTime.once)
            {
                caller.endType = ObjectActionBase.UpdateEndType.byMethod;
                using (GUIEnable.By(false))
                    EditorGUILayout.EnumPopup("End Type", ObjectActionBase.UpdateEndType.byMethod);
            }
            else
            {
                caller.endType = (ObjectActionBase.UpdateEndType)EditorGUILayout.EnumPopup(new GUIContent { text = "End Type" }, caller.endType, CheckEnum, false);
                switch (caller.endType)
                {
                    case ObjectActionBase.UpdateEndType.byCounter:
                        DrawVariable("Count", caller.count);
                        break;
                    case ObjectActionBase.UpdateEndType.byTimer:
                        DrawVariable("Duration", caller.duration);
                        break;
                    case ObjectActionBase.UpdateEndType.byMethod:
                        if (caller.actionCallTime != ObjectActionBase.ActionCallTime.once)
                        {
                            caller.endType = default;
                        }
                        break;
                    default:
                        break;
                }
            }
            EditorGUI.indentLevel--;

            bool CheckEnum(Enum arg)
            {
                if (arg is ObjectActionBase.UpdateEndType.byMethod)
                {
                    return caller.actionCallTime == ObjectActionBase.ActionCallTime.once;
                }
                return true;
            }
        }

        /// <summary>
        /// Select method
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="methods"></param>
        /// <returns></returns>
        protected string SelectMethod(string methodName)
        {
            string[] options = methods.Select(m => m.Name).ToArray();
            string result;
            GUILayout.BeginHorizontal();
            if (options.Length == 0)
            {
                EditorGUILayout.LabelField("Method Name", "No valid method found");
                result = methodName;
            }
            else
            {
                selected = UnityEditor.ArrayUtility.IndexOf(options, methodName);
                selected = Mathf.Max(selected, 0);
                selected = EditorGUILayout.Popup("Method Name", selected, options);

                result = options[selected];
            }
            if (node is IGenericMethodCaller)
            {
                if (showParentMethod)
                {
                    if (GUILayout.Button("Hide Parent Method", GUILayout.MaxWidth(200)))
                    {
                        showParentMethod = false;
                        UpdateMethods();
                    }
                }
                else if (GUILayout.Button("Display Parent Method", GUILayout.MaxWidth(200)))
                {
                    showParentMethod = true;
                    UpdateMethods();
                }
            }
            if (GUILayout.Button("...", GUILayout.MaxWidth(50)))
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Use method name for node name"), false, () => node.name = result);
                menu.ShowAsContext();
            }

            GUILayout.EndHorizontal();
            return result;
        }

        /// <summary>
        /// Draw all parameters
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="method"></param>
        protected void DrawParameters(IMethodCaller caller, MethodInfo method)
        {
            var parameterInfo = method.GetParameters();
            if (parameterInfo.Length == 0)
            {
                EditorGUILayout.LabelField("Parameters", "None");
                caller.Parameters = new List<Parameter>();
                goto validation;
            }
            EditorGUILayout.LabelField("Parameters:");
            caller.Parameters ??= new List<Parameter>();
            if (caller.Parameters.Count > parameterInfo.Length)
            {
                caller.Parameters.RemoveRange(parameterInfo.Length, caller.Parameters.Count - parameterInfo.Length);
            }
            else if (caller.Parameters.Count < parameterInfo.Length)
            {
                for (int i = caller.Parameters.Count; i < parameterInfo.Length; i++)
                {
                    caller.Parameters.Add(new Parameter());
                }
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                ParameterInfo item = parameterInfo[i];
                //Debug.Log(item);
                Parameter parameter = caller.Parameters[i];
                if (item.ParameterType == typeof(NodeProgress))
                {
                    using (GUIEnable.By(false))
                    {
                        EditorGUILayout.LabelField(item.Name.ToTitleCase() + " (Node Progress)");
                        parameter.ForceSetConstantType(VariableType.Node);
                    }
                    continue;
                }

                parameter.ParameterObjectType = item.ParameterType;
                VariableType variableType = VariableUtility.GetVariableType(item.ParameterType);
                DrawVariable(item.Name.ToTitleCase(), parameter, VariableUtility.GetCompatibleTypes(variableType));
                parameter.ForceSetConstantType(variableType);
            }
            EditorGUI.indentLevel--;
        validation:
            if (caller is ObjectActionBase action && action.endType == ObjectActionBase.UpdateEndType.byMethod && (parameterInfo.Length == 0 || parameterInfo[0].ParameterType != typeof(NodeProgress)))
            {
                EditorGUILayout.HelpBox($"Method \"{method.Name}\" should has NodeProgress as its first parameter.", MessageType.Warning);
            }
        }

        /// <summary>
        /// Draw the result variable field
        /// </summary>
        /// <param name="result"></param>
        /// <param name="method"></param>
        protected void DrawResultField(VariableReference result, MethodInfo method)
        {
            if (method.ReturnType == typeof(void))
            {
                EditorGUILayout.LabelField("Result", "void");
                result.SetReference(null);
                return;
            }
            if (method.ReturnType == typeof(IEnumerator))
            {
                EditorGUILayout.LabelField("Result", "void (Coroutine)");
                result.SetReference(null);
                return;
            }
            if (method.ReturnType == typeof(Task))
            {
                EditorGUILayout.LabelField("Result", "void (Task)");
                result.SetReference(null);
                return;
            }

            // resolve return value of Task<T>, it should be T
            Type returnType = method.ReturnType;
            if (IsTaskWithReturnValue(returnType))
            {
                returnType = returnType.GenericTypeArguments[0];
            }

            VariableType variableType = VariableUtility.GetVariableType(returnType);
            if (variableType != VariableType.Invalid)
            {
                DrawVariable($"Result ({variableType})", result, VariableUtility.GetCompatibleTypes(variableType));
            }
            else
            {
                EditorGUILayout.LabelField("Result", $"Cannot store value type {method.ReturnType.Name}");
                result.SetReference(null);
            }
        }

        /// <summary>
        /// Update possible method to select after any change was made
        /// </summary>
        protected void UpdateMethods()
        {
            Type type = node is IGenericMethodCaller genericMethodCaller
                ? genericMethodCaller.TypeReference?.ReferType
                : tree.targetScript.GetClass();
            if (node is CallGameObject) type = typeof(GameObject);
            methods = GetMethods(type, Binding);
            //Debug.Log(methods.Length);
            if (!showParentMethod) methods = methods.Where(m => m.DeclaringType == type).ToArray();
        }


        /// <summary>
        /// Get methods defined in this type
        /// </summary>
        /// <remarks>
        /// This will return only method
        /// </remarks>
        /// <param name="type"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        protected MethodInfo[] GetMethods(Type type, BindingFlags flags)
        {
            return type
                .GetMethods(flags)
                .Where(m => !m.IsSpecialName && IsValidMethod(m))
                .ToArray();
        }

        [Obsolete]
        protected MethodInfo[] GetMethods(BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            return tree.targetScript.GetClass()
                .GetMethods(flags)
                .Where(m => !m.IsSpecialName && IsValidMethod(m))
                .ToArray();
        }


        protected bool TryGetValueAndType(MemberInfo memberInfo, object target, out Type valueType, out object value, bool writeOnly = false)
        {
            valueType = null;
            value = default;
            if (memberInfo is FieldInfo fi)
            {
                if (fi.FieldType.IsSubclassOf(typeof(Component))) return false;
                if (fi.FieldType.IsSubclassOf(typeof(ScriptableObject))) return false;
                if (target != null) value = fi.GetValue(target);
                valueType = fi.FieldType;
                return true;
            }
            else if (memberInfo is PropertyInfo pi)
            {
                if (pi.PropertyType.IsSubclassOf(typeof(Component))) return false;
                if (pi.PropertyType.IsSubclassOf(typeof(ScriptableObject))) return false;
                if (writeOnly && !pi.CanWrite) return false;
                if (target != null)
                {
                    // is renderer, prefab
                    if (target is UnityEngine.Renderer r && PrefabUtility.IsPartOfAnyPrefab(r))
                    {
                        if (pi.Name == "materials" || pi.Name == "material") return false;
                    }
                    try { value = pi.GetValue(target); }
                    catch { }
                }

                valueType = pi.PropertyType;
                return true;
            }
            else return false;
        }


        /// <summary>
        /// Draw Get Field list
        /// </summary>
        /// <param name="node"></param>
        /// <param name="baseObject"></param>
        /// <param name="objectType"></param>
        protected void DrawGetFields(ObjectGetValueBase node, object baseObject, Type objectType)
        {
            GUILayoutOption changedButtonWidth = GUILayout.MaxWidth(20);
            GUILayoutOption useVariableWidth = GUILayout.MaxWidth(100);
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;


            var colorStyle = SetRegionColor(Color.white * (80 / 255f), out Color baseColor);
            fieldListScroll = GUILayout.BeginScrollView(fieldListScroll, colorStyle);
            GUI.backgroundColor = baseColor;


            foreach (var memberInfo in objectType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty))
            {
                //member is obsolete
                if (memberInfo.IsDefined(typeof(ObsoleteAttribute))) continue;
                //member is too high in the hierachy
                if (typeof(Component).IsSubclassOf(memberInfo.DeclaringType) || typeof(Component) == memberInfo.DeclaringType) continue;
                // not allow to access this
                if (baseObject is Renderer && memberInfo.Name == nameof(Renderer.material)) continue;
                if (baseObject is Renderer && memberInfo.Name == nameof(Renderer.materials)) continue;
                //properties that is readonly
                if (!TryGetValueAndType(memberInfo, baseObject, out Type valueType, out object currentValue)) continue;

                VariableType type = VariableUtility.GetVariableType(valueType);
                if (type == VariableType.Invalid || type == VariableType.Node) continue;

                GUILayout.BeginHorizontal();
                var hasEntry = node.IsEntryDefinded(memberInfo.Name);

                // already have change entry
                if (hasEntry)
                {
                    DrawVariable(memberInfo.Name.ToTitleCase(), node.GetChangeEntry(memberInfo.Name).data, new VariableType[] { type });
                    if (GUILayout.Button("X", changedButtonWidth))
                    {
                        Undo.RecordObject(tree, $"Remove entry ({memberInfo.Name}) in {node.name}");
                        node.RemoveChangeEntry(memberInfo.Name);
                    }
                }
                // no change entry
                else
                {
                    if (currentValue == null)
                    {
                        string label2 = type == VariableType.UnityObject || type == VariableType.Generic ? $"({type}: {valueType.Name})" : $"({type})";
                        EditorGUILayout.LabelField(memberInfo.Name.ToTitleCase(), label2);
                    }
                    else
                    {
                        EditorFieldDrawers.DrawField(memberInfo.Name.ToTitleCase(), ref currentValue, isReadOnly: true, displayUnsupportInfo: true, objectToUndo: tree);
                    }
                    if (GUILayout.Button("Get", useVariableWidth))
                    {
                        Undo.RecordObject(tree, $"Add new entry ({memberInfo.Name}) in {node.name}");
                        node.AddPointer(memberInfo.Name, type);
                    }
                    var prevState = GUI.enabled;
                    GUI.enabled = false;
                    GUILayout.Button("-", changedButtonWidth);
                    GUI.enabled = prevState;
                }
                GUILayout.EndHorizontal();
            }


            GUILayout.EndScrollView();
            EditorGUI.indentLevel--;
        }

        /// <summary>
        /// Draw set field list
        /// </summary>
        /// <param name="node"></param>
        /// <param name="baseObject"></param>
        /// <param name="objectType"></param>
        protected void DrawSetFields(ObjectSetValueBase node, object baseObject, Type objectType)
        {
            GUILayoutOption changedButtonWidth = GUILayout.MaxWidth(20);
            GUILayoutOption useVariableWidth = GUILayout.MaxWidth(100);
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var colorStyle = SetRegionColor(Color.white * (80 / 255f), out Color baseColor);
            fieldListScroll = GUILayout.BeginScrollView(fieldListScroll, colorStyle);
            GUI.backgroundColor = baseColor;

            foreach (var memberInfo in objectType.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetField | BindingFlags.SetProperty))
            {
                //member is obsolete
                if (memberInfo.IsDefined(typeof(ObsoleteAttribute))) continue;
                //member is too high in the hierachy
                if (typeof(Component).IsSubclassOf(memberInfo.DeclaringType) || typeof(Component) == memberInfo.DeclaringType) continue;
                //properties that is readonly
                if (!TryGetValueAndType(memberInfo, baseObject, out Type valueType, out object currentValue, true)) continue;

                VariableType type = VariableUtility.GetVariableType(valueType);
                if (type == VariableType.Invalid || type == VariableType.Node) continue;

                GUILayout.BeginHorizontal();
                var hasEntry = node.IsEntryDefinded(memberInfo.Name);

                // already have change entry
                if (hasEntry)
                {
                    Parameter data = node.GetChangeEntry(memberInfo.Name).data;
                    data.ParameterObjectType = valueType;
                    DrawVariable(memberInfo.Name.ToTitleCase(), data, VariableUtility.GetCompatibleTypes(type));
                    if (GUILayout.Button("X", changedButtonWidth))
                    {
                        Undo.RecordObject(tree, $"Remove entry ({memberInfo.Name}) in {node.name}");
                        node.RemoveChangeEntry(memberInfo.Name);
                    }
                }
                // no change entry
                else
                {
                    object newVal;
                    if (currentValue == null)
                    {
                        newVal = null;
                        string label2 = GetFieldInfo(valueType, type);
                        EditorGUILayout.LabelField(memberInfo.Name.ToTitleCase(), label2);
                    }
                    else
                    {
                        newVal = EditorFieldDrawers.DrawField(memberInfo.Name.ToTitleCase(), currentValue, isReadOnly: false, displayUnsupportInfo: true);
                    }
                    if (currentValue != null && !currentValue.Equals(newVal))
                    {
                        Undo.RecordObject(tree, $"Add new entry ({memberInfo.Name}) in {node.name} and set to {newVal}");
                        node.AddChangeEntry(memberInfo.Name, valueType);
                    }
                    if (GUILayout.Button("Modify", useVariableWidth))
                    {
                        Undo.RecordObject(tree, $"Add new entry ({memberInfo.Name}) in {node.name}");
                        node.AddChangeEntry(memberInfo.Name, valueType);
                    }
                    var prevState = GUI.enabled;
                    GUI.enabled = false;
                    GUILayout.Button("-", changedButtonWidth);
                    GUI.enabled = prevState;
                }
                GUILayout.EndHorizontal();
            }


            GUILayout.EndScrollView();
            EditorGUI.indentLevel--;
        }

        private static string GetFieldInfo(Type valueType, VariableType type)
        {
            string label2 = $"({type})";
            if (type == VariableType.UnityObject || type == VariableType.Generic)
            {
                label2 = $"({type}: {valueType.Name})";
            }
            else
            {
                var defaultType = VariableUtility.GetType(type);
                if (!valueType.IsSubclassOf(defaultType) && valueType != defaultType)
                {
                    label2 = $"({type}: {valueType.Name})";
                }
            }

            return label2;
        }

        /// <summary>
        /// Check is method a valid action method
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        protected bool IsValidActionMethod(MethodInfo m)
        {
            if (m.IsGenericMethod) return false;
            if (m.IsGenericMethodDefinition) return false;
            if (m.ContainsGenericParameters) return false;
            if (Attribute.IsDefined(m, typeof(ObsoleteAttribute))) return false;
            ObjectActionBase ObjectAction = node as ObjectActionBase;

            ParameterInfo[] parameterInfos = m.GetParameters();
            // no argument function can only be task or IEnumerator(Coroutine)
            if (parameterInfos.Length == 0)
            {
                // by method return, then require to be task or coroutine
                if (ObjectAction.endType != ObjectActionBase.UpdateEndType.byMethod)
                {
                    return true;
                }
                return IsTaskOrCoroutine(m);
            }

            // not start with NodeProgress
            if (parameterInfos[0].ParameterType != typeof(NodeProgress))
            {
                //by method, but method does not start with node progress
                if (ObjectAction.endType == ObjectActionBase.UpdateEndType.byMethod && !IsTaskOrCoroutine(m))
                {
                    return false;
                }
                //not by method, but first argument is invalid
                else if (VariableUtility.GetVariableType(parameterInfos[0].ParameterType) == VariableType.Invalid)
                {
                    return false;
                }
            }

            //check 1+ param
            for (int i = 1; i < parameterInfos.Length; i++)
            {
                ParameterInfo item = parameterInfos[i];
                VariableType variableType = VariableUtility.GetVariableType(item.ParameterType);
                if (variableType == VariableType.Invalid || variableType == VariableType.Node)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsTaskOrCoroutine(MethodInfo m)
        {
            Type type = m.ReturnType;
            return type == typeof(Task) || IsTaskWithReturnValue(type) || typeof(IEnumerator).IsAssignableFrom(type);
        }

        private static bool IsTaskWithReturnValue(Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>));
        }

        /// <summary>
        /// Draw Method data for ObjectActions
        /// </summary>
        /// <param name="action"></param>
        protected void DrawActionMethodData(ObjectActionBase action)
        {
            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            //GUILayout.Space(EditorGUIUtility.singleLineHeight);
            action.methodName = SelectMethod(action.methodName);

            var method = methods.FirstOrDefault(m => m.Name == action.methodName);
            if (method is null)
            {
                action.actionCallTime = ObjectActionBase.ActionCallTime.fixedUpdate;
                action.endType = ObjectActionBase.UpdateEndType.byCounter;
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }
            DrawParameters(action, method);
            DrawResultField(action.result, method);
            EditorGUI.indentLevel--;
        }

    }
}