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
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public abstract partial class MethodCallerDrawerBase : NodeDrawerBase
    {
        /// <summary>
        /// Binding flags for instance method.
        /// </summary>
        protected const BindingFlags INSTANCE_MEMBER = BindingFlags.Public | BindingFlags.Instance;

        /// <summary>
        /// Binding flags for static method.
        /// </summary>
        protected const BindingFlags STATIC_MEMBER = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        protected static VariableType[] UNITY_OBJECT_VARIABLE_TYPE = new VariableType[] { VariableType.UnityObject, VariableType.Generic };

        /// <summary>
        /// Serialized field names for method caller nodes.
        /// </summary>  
        private const string TypePropertyName = nameof(ObjectAction.type);

        protected static readonly GUIContent label = new("Type");

        protected bool showParentMethod;
        protected int selected;
        protected Type refType;
        protected MethodInfo[] methods;
        internal TypeReferenceDrawer typeReferenceDrawer;
        protected Vector2 fieldListScroll;

        private TreeViewState getFieldTreeViewState;
        private GetFieldTreeView getFieldTreeView;

        private static readonly Dictionary<Type, string> EntryListPropertyCache = new();

        protected virtual BindingFlags Binding => INSTANCE_MEMBER;

        /// <summary>
        /// Check whether method is valid.
        /// </summary>
        /// <param name="method">Method info.</param>
        /// <returns>True if the method is a valid candidate.</returns>
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
                if (variableType == VariableType.Node && (i != 0 || (item.ParameterType != typeof(CancellationToken) && item.ParameterType != typeof(NodeProgress))))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Draw component selection using serialized properties when available.
        /// </summary>
        /// <param name="caller">Component caller.</param>
        /// <returns>True if the component selection is valid.</returns>
        protected bool DrawComponent()
        {
            SerializedProperty getComponentProperty = FindRelativeProperty(nameof(ComponentCall.getComponent));
            SerializedProperty componentProperty = FindRelativeProperty(nameof(ComponentCall.component));
            SerializedProperty typeProperty = FindRelativeProperty(nameof(ObjectAction.type));

            if (getComponentProperty == null || componentProperty == null || typeProperty == null)
            {
                // error info
                EditorGUILayout.HelpBox("Cannot find component properties.", MessageType.Error);
                return false;
            }

            EditorGUILayout.LabelField("Component Data", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(getComponentProperty, new GUIContent("Get Component"));
            bool getComponent = getComponentProperty.boolValue;

            if (!getComponent)
            {
                UUID oldReferVar = GetVariableUuid(componentProperty);
                VariableBase componentVariable = EnsureVariableProperty(componentProperty, () => new VariableReference());
                DrawVariableProperty(new GUIContent("Component"), componentProperty, componentVariable, new VariableType[] { VariableType.UnityObject, VariableType.Generic }, VariableAccessFlag.Read);

                VariableReference variableReference = componentProperty.GetValue() as VariableReference;
                VariableData variableData = tree.GetVariable(variableReference?.UUID ?? UUID.Empty);
                if (variableData != null && oldReferVar != variableData.UUID)
                {
                    SetTypeReferenceProperty(typeProperty, variableData.ObjectType);
                }

                if (variableReference != null && !variableReference.HasEditorReference)
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
        /// Draw object selection using serialized properties when available.
        /// </summary>
        /// <param name="objectType">Resolved object type.</param>
        /// <param name="variableAccessFlag">Access constraint.</param>
        /// 
        /// <returns>True if selection is valid.</returns>
        protected bool DrawObject(SerializedProperty objectProperty, SerializedProperty typeProperty, out Type objectType, VariableAccessFlag variableAccessFlag = VariableAccessFlag.Read)
        {
            objectType = null;
            if (objectProperty == null || typeProperty == null)
            {
                return false;
            }

            EditorGUILayout.LabelField("Object Data", EditorStyles.boldLabel);
            using (EditorGUIIndent.Increase)
            {
                DrawVariableProperty(new GUIContent("Object"), objectProperty, null, variableAccessFlag);

                VariableReference objectReference = objectProperty.GetValue() as VariableReference;
                VariableData variableData = tree.GetVariable(objectReference?.UUID ?? UUID.Empty);
                if (variableData != null
                    && typeProperty.boxedValue is TypeReference typeReference
                    && typeReference.HasReferType
                    && variableData.ObjectType != null
                    && !typeReference.IsSubclassOf(variableData.ObjectType))
                {
                    SetTypeReferenceProperty(typeProperty, variableData.ObjectType);
                }

                if (objectReference != null && !objectReference.HasEditorReference)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("No Object Assigned");
                    return true;
                }

                EditorGUILayout.PropertyField(typeProperty, label, true);

                GUILayout.BeginHorizontal();
                GUILayout.Space(30);
                if (GUILayout.Button("Use variable default type"))
                {
                    SetTypeReferenceProperty(typeProperty, variableData?.ObjectType);
                }
                GUILayout.EndHorizontal();

                typeReference = typeProperty.boxedValue as TypeReference;
                objectType = typeReference?.ReferType;
                if (objectType == null)
                {
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField("Type is not valid");
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Draw reference type selection for method callers.
        /// </summary>
        /// <param name="bindings">Binding flags to query methods.</param>
        /// 
        /// <returns>True if a valid type is selected.</returns>
        protected bool DrawReferType(BindingFlags bindings)
        {
            SerializedProperty typeProperty = FindRelativeProperty(TypePropertyName);
            if (typeProperty == null)
            {
                return false;
            }
            IGenericMethodCaller caller = node as IGenericMethodCaller;
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(typeProperty, label, true);

            TypeReference typeReference = typeProperty.GetValue() as TypeReference;
            GenericMenu menu = new();
            if (tree.targetScript)
            {
                menu.AddItem(new GUIContent("Use Target Script Type"), false, () => SetTypeReferenceProperty(typeProperty, tree.targetScript.GetClass()));
            }
            if (node is IComponentCaller ccer && !ccer.GetComponent)
            {
                menu.AddItem(new GUIContent("Use Variable Type"), false, () => SetTypeReferenceProperty(typeProperty, tree.GetVariableType(ccer.Component.UUID)));
            }
            RightClickMenu(menu);

            if (typeReference?.ReferType == null)
            {
                EditorGUILayout.LabelField("Cannot load type");
                EditorGUI.indentLevel--;
                return false;
            }

            if (typeReference.ReferType != refType)
            {
                methods = GetMethods(typeReference.ReferType, bindings);
                if (!showParentMethod)
                {
                    var selfDeclared = methods.Where(m => m.DeclaringType == typeReference.ReferType).ToArray();
                    if (Array.IndexOf(selfDeclared, caller.MethodName) != -1)
                    {
                        methods = selfDeclared.ToArray();
                    }
                    else
                    {
                        showParentMethod = true;
                    }
                }

                refType = typeReference.ReferType;
            }

            EditorGUI.indentLevel--;
            return true;
        }

        /// <summary>
        /// Draw action data using serialized properties when available.
        /// </summary>
        /// <param name="caller">Action node.</param>
        protected void DrawActionData()
        {
            SerializedProperty actionCallTimeProperty = FindRelativeProperty(nameof(ObjectActionBase.actionCallTime));
            SerializedProperty endTypeProperty = FindRelativeProperty(nameof(ObjectActionBase.endType));
            SerializedProperty countProperty = FindRelativeProperty(nameof(ObjectActionBase.count));
            SerializedProperty durationProperty = FindRelativeProperty(nameof(ObjectActionBase.duration));

            if (actionCallTimeProperty == null || endTypeProperty == null || countProperty == null || durationProperty == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Action Execution", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(actionCallTimeProperty, new GUIContent("Action Call Time"));
            ObjectActionBase.ActionCallTime actionCallTime = (ObjectActionBase.ActionCallTime)actionCallTimeProperty.enumValueIndex;

            if (actionCallTime == ObjectActionBase.ActionCallTime.once)
            {
                endTypeProperty.enumValueIndex = (int)ObjectActionBase.UpdateEndType.byMethod;
                using (GUIEnable.By(false))
                    EditorGUILayout.EnumPopup("End Type", ObjectActionBase.UpdateEndType.byMethod);
            }
            else
            {
                ObjectActionBase.UpdateEndType endType = (ObjectActionBase.UpdateEndType)endTypeProperty.enumValueIndex;
                endType = (ObjectActionBase.UpdateEndType)EditorGUILayout.EnumPopup(new GUIContent("End Type"), endType, CheckEnum, false);
                endTypeProperty.enumValueIndex = (int)endType;

                switch (endType)
                {
                    case ObjectActionBase.UpdateEndType.byCounter:
                        DrawVariableProperty(new GUIContent("Count"), countProperty, null, VariableAccessFlag.Read);
                        break;
                    case ObjectActionBase.UpdateEndType.byTimer:
                        DrawVariableProperty(new GUIContent("Duration"), durationProperty, null, VariableAccessFlag.Read);
                        break;
                    case ObjectActionBase.UpdateEndType.byMethod:
                        if (actionCallTime != ObjectActionBase.ActionCallTime.once)
                        {
                            endTypeProperty.enumValueIndex = default;
                        }
                        break;
                }
            }

            EditorGUI.indentLevel--;

            bool CheckEnum(Enum arg)
            {
                if (arg is ObjectActionBase.UpdateEndType.byMethod)
                {
                    return actionCallTime == ObjectActionBase.ActionCallTime.once;
                }
                return true;
            }
        }

        /// <summary>
        /// Select method from current method list.
        /// </summary>
        /// <param name="methodNameProperty">Current method name.</param>
        /// <returns>Selected method name.</returns> 
        protected MethodInfo SelectMethod(SerializedProperty methodNameProperty)
        {
            string methodName = methodNameProperty.stringValue;
            string[] options = methods.Select(m => m.Name).ToArray();
            string result;

            using (new GUILayout.HorizontalScope())
            {
                if (options.Length == 0)
                {
                    EditorGUILayout.LabelField("Method Name", "No valid method found");
                    result = methodName;
                }
                else
                {
                    selected = UnityEditor.ArrayUtility.IndexOf(options, methodName);
                    selected = Mathf.Max(selected, 0);
                    var newSelection = EditorGUILayout.Popup("Method Name", selected, options);
                    result = options[newSelection];
                    if (newSelection != selected)
                    {
                        selected = newSelection;
                        methodNameProperty.stringValue = result;
                        methodNameProperty.serializedObject.ApplyModifiedProperties();
                    }
                }

                if (node is IGenericMethodCaller)
                {
                    var text = showParentMethod ? "Hide Parent Method" : "Display Parent Method";
                    if (GUILayout.Button(text, GUILayout.MaxWidth(200)))
                    {
                        showParentMethod = !showParentMethod;
                        UpdateMethods();
                    }
                }
                if (GUILayout.Button("...", GUILayout.MaxWidth(50)))
                {
                    GenericMenu menu = new();
                    menu.AddItem(new GUIContent("Use method name for node name"), false, () => node.name = result);
                    menu.ShowAsContext();
                }
            }

            var method = methods.FirstOrDefault(m => m.Name == result);
            return method;
        }

        /// <summary>
        /// Draw all parameters using serialized properties when available.
        /// </summary>
        /// <param name="method">Target method.</param> 
        protected void DrawParameters(MethodInfo method)
        {
            SerializedProperty parametersProperty = FindRelativeProperty(nameof(ObjectActionBase.parameters));
            if (parametersProperty == null || !parametersProperty.isArray)
            {
                // error info
                EditorGUILayout.HelpBox("Cannot find parameters property.", MessageType.Error);
                return;
            }

            var parameterInfo = method.GetParameters();
            if (parameterInfo.Length == 0)
            {
                EditorGUILayout.LabelField("Parameters", "None");
                parametersProperty.arraySize = 0;
                goto validation;
            }

            EditorGUILayout.LabelField("Parameters:");

            if (parametersProperty.arraySize != parameterInfo.Length)
            {
                parametersProperty.arraySize = parameterInfo.Length;
            }

            EditorGUI.indentLevel++;
            for (int i = 0; i < parameterInfo.Length; i++)
            {
                ParameterInfo item = parameterInfo[i];
                SerializedProperty parameterProperty = parametersProperty.GetArrayElementAtIndex(i);
                Parameter parameter = parameterProperty.GetValue() as Parameter ?? new Parameter();
                parameter.ParameterObjectType = item.ParameterType;

                if (item.ParameterType == typeof(NodeProgress))
                {
                    using (GUIEnable.By(false))
                    {
                        EditorGUILayout.LabelField(item.Name.ToTitleCase() + " (Node Progress)");
                        ForceSetParameterType(parameterProperty, parameter, VariableType.Node);
                    }
                    continue;
                }
                if (item.ParameterType == typeof(CancellationToken))
                {
                    using (GUIEnable.By(false))
                    {
                        EditorGUILayout.LabelField(item.Name.ToTitleCase() + " (Cancellation Token)");
                        ForceSetParameterType(parameterProperty, parameter, VariableType.Node);
                    }
                    continue;
                }

                VariableType variableType = VariableUtility.GetVariableType(item.ParameterType);
                ForceSetParameterType(parameterProperty, parameter, variableType);

                DrawVariableProperty(new GUIContent(item.Name.ToTitleCase()), parameterProperty, parameter, VariableUtility.GetCompatibleTypes(variableType), VariableAccessFlag.None);
            }
            EditorGUI.indentLevel--;

        validation:
            if (node is ObjectActionBase action && action.endType == ObjectActionBase.UpdateEndType.byMethod)
            {
                if (parameterInfo.Length == 0)
                {
                    if (!IsTaskOrCoroutine(method)) EditorGUILayout.HelpBox($"Method \"{method.Name}\" should has NodeProgress as its first parameter.", MessageType.Warning);
                    else EditorGUILayout.HelpBox($"Method \"{method.Name}\" should has NodeProgress/CancellationToken as its first parameter.", MessageType.Warning);
                }
                else if (!IsTaskOrCoroutine(method))
                {
                    if (parameterInfo[0].ParameterType != typeof(NodeProgress))
                        EditorGUILayout.HelpBox($"Method \"{method.Name}\" should has NodeProgress as its first parameter.", MessageType.Warning);
                }
                else if (parameterInfo[0].ParameterType != typeof(NodeProgress) && parameterInfo[0].ParameterType != typeof(CancellationToken))
                {
                    EditorGUILayout.HelpBox($"Method \"{method.Name}\" should has NodeProgress/CancellationToken as its first parameter.", MessageType.Warning);
                }
            }

            static void ForceSetParameterType(SerializedProperty parameterProperty, Parameter parameter, VariableType type)
            {
                if (parameter.Type == type) return;

                parameter.ForceSetConstantType(type);
                parameterProperty.boxedValue = parameter;
                parameterProperty.serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Update possible method list after a type change.
        /// </summary>
        protected void UpdateMethods()
        {
            Type type = node is IGenericMethodCaller genericMethodCaller
                ? genericMethodCaller.TypeReference?.ReferType
                : tree.targetScript.GetClass();
            if (node is CallGameObject) type = typeof(GameObject);
            methods = GetMethods(type, Binding);
            if (!showParentMethod) methods = methods.Where(m => m.DeclaringType == type).ToArray();
        }

        /// <summary>
        /// Get methods defined in the given type.
        /// </summary>
        /// <param name="type">Target type.</param>
        /// <param name="flags">Binding flags.</param>
        /// <returns>Matching methods.</returns>
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






        protected SerializedProperty FindRelativeProperty(string propertyName) => property?.FindPropertyRelative(propertyName);

        private static UUID GetVariableUuid(SerializedProperty variableProperty)
        {
            return variableProperty?.GetValue() is VariableReference variable ? variable.UUID : UUID.Empty;
        }

        /// <summary>
        /// Apply a variable instance back to its serialized property.
        /// </summary>
        /// <param name="variableProperty">Serialized property.</param>
        /// <param name="variable">Variable instance.</param>
        private void ApplyVariableProperty(SerializedProperty variableProperty, VariableBase variable)
        {
            if (variableProperty == null || variable == null)
            {
                return;
            }

            variableProperty.serializedObject.Update();
            variableProperty.boxedValue = variable;
            variableProperty.serializedObject.ApplyModifiedProperties();
            variableProperty.serializedObject.Update();
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
        /// Ensure a variable property has a valid instance.
        /// </summary>
        /// <param name="variableProperty">Serialized property.</param>
        /// <param name="factory">Factory function to create a variable.</param>
        /// <returns>Resolved variable instance.</returns>
        private VariableBase EnsureVariableProperty(SerializedProperty variableProperty, Func<VariableBase> factory)
        {
            if (variableProperty == null)
            {
                return null;
            }

            if (variableProperty.GetValue() is VariableBase variable)
            {
                return variable;
            }

            variable = factory?.Invoke();
            ApplyVariableProperty(variableProperty, variable);
            return variable;
        }

        /// <summary>
        /// Draw a variable field using serialized property tracking.
        /// </summary>
        /// <param name="label">Label for the field.</param>
        /// <param name="variableProperty">Serialized property.</param>
        /// <param name="variable">Variable instance.</param>
        /// <param name="possibleTypes">Allowed variable types.</param>
        /// <param name="variableAccessFlag">Access constraint.</param>
        private void DrawVariableProperty(GUIContent label, SerializedProperty variableProperty, VariableBase variable, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag)
        {
            DrawVariableProperty(label, variableProperty, possibleTypes, variableAccessFlag);
        }

        private void DrawVariableProperty(GUIContent label, SerializedProperty variableProperty, VariableType[] possibleTypes, VariableAccessFlag variableAccessFlag)
        {
            if (variableProperty?.boxedValue is not VariableBase variable || variableProperty == null)
            {
                return;
            }

            float height = VariableFieldDrawers.GetVariableHeight(variable, tree, possibleTypes, variableAccessFlag);
            Rect rect = EditorGUILayout.GetControlRect(true, height);

            EditorGUI.BeginChangeCheck();
            VariableFieldDrawers.DrawVariable(rect, label, variable, tree, possibleTypes, variableAccessFlag);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyVariableProperty(variableProperty, variable);
            }

            return;
        }

        /// <summary>
        /// Clear a variable reference property.
        /// </summary>
        /// <param name="resultProperty">Serialized property to clear.</param>
        private void ClearVariableReference(SerializedProperty resultProperty)
        {
            if (resultProperty?.GetValue() is VariableReference resultReference)
            {
                resultReference.SetReference(null);
                ApplyVariableProperty(resultProperty, resultReference);
            }
        }






        /// <summary>
        /// Draw Get Field list
        /// </summary>
        /// <param name="node">Target node.</param>
        /// <param name="baseObject">Object instance used to preview values.</param>
        /// <param name="objectType">Resolved object type.</param>
        protected void DrawGetFields(ObjectGetValueBase node, SerializedProperty entryListProperty, object baseObject, Type objectType)
        {
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            using (EditorGUIIndent.Increase)
            {
                if (objectType == null)
                {
                    EditorGUILayout.HelpBox("Cannot resolve object type for fields.", MessageType.Warning);
                    return;
                }

                if (entryListProperty == null)
                {
                    EditorGUILayout.HelpBox("Cannot locate serialized entry list for get fields.", MessageType.Warning);
                    return;
                }

                getFieldTreeViewState ??= new TreeViewState();
                getFieldTreeView ??= new GetFieldTreeView(getFieldTreeViewState, this);

                getFieldTreeView.SetData(node, baseObject, objectType, entryListProperty);
                float height = Mathf.Max(150f, getFieldTreeView.TotalHeight + 6f);
                Rect rect = GUILayoutUtility.GetRect(0f, 100000f, 0f, height);
                getFieldTreeView.OnGUI(rect);
            }
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
        /// Set a type reference on a serialized property.
        /// </summary>
        /// <param name="typeProperty">Serialized property containing a type reference.</param>
        /// <param name="type">Type to apply.</param>
        protected void SetTypeReferenceProperty(SerializedProperty typeProperty, Type type)
        {
            if (typeProperty?.GetValue() is not TypeReference typeReference)
            {
                return;
            }

            typeReference.SetReferType(type);
            typeProperty.serializedObject.Update();
            typeProperty.boxedValue = typeReference;
            typeProperty.serializedObject.ApplyModifiedProperties();
            typeProperty.serializedObject.Update();
        }

        protected bool DrawResultField(SerializedProperty resultProperty, MethodInfo method)
        {
            if (resultProperty == null)
            {
                return false;
            }

            if (method.ReturnType == typeof(void))
            {
                EditorGUILayout.LabelField("Result", "void");
                ClearVariableReference(resultProperty);
                return true;
            }
            if (method.ReturnType == typeof(IEnumerator))
            {
                EditorGUILayout.LabelField("Result", "void (Coroutine)");
                ClearVariableReference(resultProperty);
                return true;
            }
            if (method.ReturnType == typeof(Task))
            {
                EditorGUILayout.LabelField("Result", "void (Task)");
                ClearVariableReference(resultProperty);
                return true;
            }
            if (method.ReturnType == typeof(Awaitable))
            {
                EditorGUILayout.LabelField("Result", "void (Awaitable)");
                ClearVariableReference(resultProperty);
                return true;
            }

            Type returnType = method.ReturnType;
            if (IsTaskWithReturnValue(returnType) || returnType == typeof(Awaitable<bool>))
            {
                returnType = returnType.GenericTypeArguments[0];
            }

            VariableType variableType = VariableUtility.GetVariableType(returnType);
            if (variableType != VariableType.Invalid)
            {
                VariableBase resultVariable = EnsureVariableProperty(resultProperty, () => new VariableReference());
                DrawVariableProperty(new GUIContent($"Result ({variableType})"), resultProperty, resultVariable, VariableUtility.GetCompatibleTypes(variableType), VariableAccessFlag.Read);
            }
            else
            {
                EditorGUILayout.LabelField("Result", $"Cannot store value type {method.ReturnType.Name}");
                ClearVariableReference(resultProperty);
            }

            return true;
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
            ObjectActionBase objectAction = node as ObjectActionBase;

            ParameterInfo[] parameterInfos = m.GetParameters();
            // no argument function can only be task or IEnumerator(Coroutine)
            if (parameterInfos.Length == 0)
            {
                // by method return, then require to be task or coroutine
                if (objectAction.endType != ObjectActionBase.UpdateEndType.byMethod)
                {
                    return true;
                }
                return IsTaskOrCoroutine(m);
            }

            // not start with NodeProgress
            if (parameterInfos[0].ParameterType != typeof(NodeProgress))
            {
                //by method, but method does not start with node progress
                if (objectAction.endType == ObjectActionBase.UpdateEndType.byMethod && !IsTaskOrCoroutine(m))
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
            return type == typeof(Task) || IsTaskWithReturnValue(type) || type == typeof(Awaitable) || type == typeof(Awaitable<bool>) || typeof(IEnumerator).IsAssignableFrom(type);
        }

        private static bool IsTaskWithReturnValue(Type type)
        {
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>)) || type == typeof(Awaitable<bool>);
        }

        /// <summary>
        /// Draw Method data for ObjectActions
        /// </summary>
        /// <param name="action"></param>
        protected void DrawActionMethodData()
        {
            ObjectActionBase action = node as ObjectActionBase;

            EditorGUILayout.LabelField("Method", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            //GUILayout.Space(EditorGUIUtility.singleLineHeight); 

            var method = SelectMethod(property.FindPropertyRelative(nameof(ObjectActionBase.methodName)));
            if (method is null)
            {
                action.actionCallTime = ObjectActionBase.ActionCallTime.fixedUpdate;
                action.endType = ObjectActionBase.UpdateEndType.byCounter;
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }
            DrawParameters(method);
            DrawResultField(property.FindPropertyRelative(nameof(ObjectActionBase.result)), method);
            EditorGUI.indentLevel--;
        }
    }
}
