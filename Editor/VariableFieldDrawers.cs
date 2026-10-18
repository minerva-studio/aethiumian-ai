using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static Aethiumian.AI.Variables.VariableData;
using static Aethiumian.AI.Variables.VariableUtility;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Drawer of variables
    /// <br/>
    /// Author : Wendell Cai
    /// </summary>
    public static class VariableFieldDrawers
    {
        private const float FieldSpacing = 4f;
        private const float MinimumValueWidth = 90f;
        private const float MaximumLabelWidth = 90f;
        private const float TypeWidth = 64f;
        private const string TruncationSuffix = "…";
        private static readonly VariableType[] ALL_VARIABLES = (VariableType[])Enum.GetValues(typeof(VariableType));

        /// <summary>Temporarily enables Unity's single-row vector layout.</summary>
        private readonly struct WideModeScope : IDisposable
        {
            private readonly bool previousWideMode;

            public WideModeScope(bool forceWideMode)
            {
                previousWideMode = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = forceWideMode;
            }

            public void Dispose()
            {
                EditorGUIUtility.wideMode = previousWideMode;
            }
        }

        /// <summary>Resolves an explicit usage requirement or a fixed field declaration without constraining dynamic fields to object.</summary>
        /// <param name="variable">The field whose fixed declaration can supply a default requirement.</param>
        /// <param name="explicitConsumerType">The usage's CLR type, or null when no context was supplied.</param>
        /// <param name="consumerType">The consumer type on success; null on failure. Inspect the return value before using it.</param>
        /// <returns>True when a consumer is known, including an explicit object consumer; false for an unconstrained dynamic field.</returns>
        internal static bool TryResolveConsumerType(VariableFieldBase variable, Type explicitConsumerType, out Type consumerType)
        {
            if (explicitConsumerType != null)
            {
                consumerType = explicitConsumerType;
                return true;
            }
            if (variable != null && variable is not IDynamicVariableField)
            {
                consumerType = variable.DeclaredValueType;
                return true;
            }
            consumerType = null;
            return false;
        }

        private static Rect GetRowRect(Rect position)
        {
            Rect row = position;
            row.height = EditorGUIUtility.singleLineHeight;
            // position should already be indented
            return row;
        }

        internal readonly struct VariableRowLayout
        {
            internal Rect LabelRect { get; }
            internal Rect ValueRect { get; }
            internal Rect TypeRect { get; }
            internal Rect OverflowRect { get; }
            internal bool HasOverflow { get; }

            internal VariableRowLayout(Rect labelRect, Rect valueRect, Rect typeRect, Rect overflowRect, bool hasOverflow)
            {
                LabelRect = labelRect;
                ValueRect = valueRect;
                TypeRect = typeRect;
                OverflowRect = overflowRect;
                HasOverflow = hasOverflow;
            }
        }

        /// <summary>Calculates the fixed label, value, type, and action regions of one variable row.</summary>
        /// <remarks>The row never wraps or reorders. The type column is a compact fixed category column, the label yields its space before the value does, and zero-sized rects keep extremely narrow rows inside their bounds.</remarks>
        internal static VariableRowLayout CalculateRowLayout(Rect position, bool hasAction)
        {
            float width = Mathf.Max(0f, position.width);
            float right = position.xMax;

            float overflowWidth = hasAction ? Mathf.Min(GraphInspectorLayout.OverflowWidth, width) : 0f;
            Rect overflow = hasAction
                ? new Rect(right - overflowWidth, position.y, overflowWidth, position.height)
                : Rect.zero;
            if (hasAction)
            {
                right = overflow.x;
                if (right > position.x)
                {
                    right = Mathf.Max(position.x, right - FieldSpacing);
                }
            }

            float typeWidth = Mathf.Min(TypeWidth, Mathf.Max(0f, right - position.x));
            Rect type = new(right - typeWidth, position.y, typeWidth, position.height);
            if (typeWidth > 0f)
            {
                right = type.x;
                if (right > position.x)
                {
                    right = Mathf.Max(position.x, right - FieldSpacing);
                }
            }

            float pairWidth = Mathf.Max(0f, right - position.x);
            // The value keeps a usable name/editor width; the label only takes the remaining capped share.
            float minimumValueWidth = Mathf.Min(MinimumValueWidth, pairWidth);
            float maximumLabelWidth = Mathf.Min(width * 0.25f, MaximumLabelWidth);
            float labelWidth = Mathf.Min(
                maximumLabelWidth,
                Mathf.Max(0f, pairWidth - minimumValueWidth - FieldSpacing));
            Rect label = new(position.x, position.y, labelWidth, position.height);
            float valueX = labelWidth > 0f ? label.xMax + FieldSpacing : position.x;
            Rect value = new(valueX, position.y, Mathf.Max(0f, right - valueX), position.height);
            return new VariableRowLayout(label, value, type, overflow, hasAction);
        }

        /// <summary>Describes the current value type derived for one draw pass; a null actual type means the authored binding or declared type has no usable value type.</summary>
        internal readonly struct CurrentVariableType
        {
            internal Type ActualType { get; }
            internal VariableType Category => ActualType == null ? VariableType.Invalid : VariableTypeCatalog.Of(ActualType);

            internal CurrentVariableType(Type actualType)
            {
                ActualType = actualType;
            }
        }

        /// <summary>One compatible type-menu entry; compatibility is the authored-menu fact consumed by drawing.</summary>
        internal readonly struct VariableTypeMenuOption
        {
            internal VariableType Type { get; }
            internal Type ActualType { get; }
            internal bool IsCurrent { get; }
            internal bool IsCompatible { get; }

            internal VariableTypeMenuOption(VariableType type, Type actualType, bool isCurrent, bool isCompatible)
            {
                Type = type;
                ActualType = actualType;
                IsCurrent = isCurrent;
                IsCompatible = isCompatible;
            }
        }

        /// <summary>How a type-menu entry participates in the current field.</summary>
        internal enum TypeMenuOptionState
        {
            Disabled,
            Current,
            ViewOnly,
            Switchable,
        }

        /// <summary>One selector entry, including a preserved invalid binding that cannot itself be chosen as a candidate.</summary>
        internal enum VariableSelectionKind
        {
            InvalidCurrent,
            None,
            Candidate,
            Create,
        }

        /// <summary>One variable-selector entry consumed by the popup.</summary>
        internal readonly struct VariableSelectionOption
        {
            internal VariableSelectionKind Kind { get; }
            internal VariableData Variable { get; }
            internal GUIContent Content { get; }
            internal UUID Uuid => Variable == null ? UUID.Empty : Variable.UUID;

            internal VariableSelectionOption(VariableSelectionKind kind, VariableData variable, GUIContent content)
            {
                Kind = kind;
                Variable = variable;
                Content = content;
            }
        }

        /// <summary>The ordered selector entries and the entry that represents the authored current state.</summary>
        internal readonly struct VariableSelectionOptions
        {
            internal IReadOnlyList<VariableSelectionOption> Options { get; }
            internal int CurrentIndex { get; }

            internal VariableSelectionOptions(IReadOnlyList<VariableSelectionOption> options, int currentIndex)
            {
                Options = options;
                CurrentIndex = currentIndex;
            }
        }

        /// <summary>Resolves the authored value type in effect for this draw pass without writing field or asset state.</summary>
        /// <param name="tree">The owning tree, used to resolve reflected script members.</param>
        /// <param name="variable">The field being drawn.</param>
        /// <remarks>Bound variables use the real variable or script-member CLR type, never the field's consumer type. Constants use their declared generic type or stored category.</remarks>
        internal static CurrentVariableType ResolveCurrentVariableType(BehaviourTreeData tree, VariableFieldBase variable)
        {
            if (variable == null)
            {
                return default;
            }

            if (variable.HasEditorReference)
            {
                VariableData data = FindVariable(tree, variable.UUID);
                return new CurrentVariableType(ResolveVariableActualType(data, tree));
            }

            Type declaredType = variable.DeclaredValueType;
            if (variable is not IDynamicVariableField && IsSupportedAccessorType(declaredType))
            {
                return new CurrentVariableType(declaredType);
            }

            Type storedType = VariableUtility.GetType(variable.Type);
            return new CurrentVariableType(IsSupportedAccessorType(storedType) ? storedType : null);
        }

        /// <summary>Resolves a variable's real CLR value type independently of the read/write directions a field needs.</summary>
        /// <param name="data">The authored variable, or null when it is missing.</param>
        /// <param name="tree">The owning tree that provides the target script class.</param>
        /// <returns>The reflected member or stored CLR type; null when the variable or member cannot be resolved.</returns>
        internal static Type ResolveVariableActualType(VariableData data, BehaviourTreeData tree)
        {
            if (data == null)
            {
                return null;
            }

            if (data.UUID == VariableData.targetScript)
            {
                return GetTargetScriptClass(tree);
            }

            if (!data.IsScript)
            {
                Type objectType = data.ObjectType;
                return IsSupportedAccessorType(objectType) ? objectType : null;
            }

            Type targetClass = GetTargetScriptClass(tree);
            MemberInfo member = FindScriptMember(targetClass, data.Path);
            return VariableMemberInfo.TryResolve(member, out VariableMemberInfo resolved) ? resolved.Type : null;
        }

        /// <summary>Builds the visible type-menu entries without performing any menu action.</summary>
        /// <param name="currentType">The already-resolved current value type for this draw pass.</param>
        internal static IReadOnlyList<VariableTypeMenuOption> BuildTypeMenuOptions(
            VariableFieldBase variable,
            IReadOnlyList<VariableType> visibleTypes,
            Type consumerType,
            VariableAccessFlag variableAccessFlag,
            CurrentVariableType currentType)
        {
            List<VariableTypeMenuOption> options = new();
            if (variable == null || visibleTypes == null)
            {
                return options;
            }

            // Only a switchable dynamic constant can create a constant type; Generic has no constant editor.
            bool hideGeneric = CanSwitchConstantType(variable);
            bool canCompareConsumer = consumerType == null || IsSupportedAccessorType(consumerType);
            VariableType currentCategory = currentType.Category;
            HashSet<VariableType> seen = new();
            for (int i = 0; i < visibleTypes.Count; i++)
            {
                VariableType type = visibleTypes[i];
                if (!IsSelectableVariableCategory(type)
                    || (hideGeneric && type == VariableType.Generic)
                    || !seen.Add(type))
                {
                    continue;
                }

                bool isCurrent = currentType.ActualType != null && type == currentCategory;
                Type optionType = isCurrent ? currentType.ActualType : VariableUtility.GetType(type);
                bool isCompatible = consumerType == null
                    || (canCompareConsumer
                        && IsSupportedAccessorType(optionType)
                        && VariableTypeCatalog.SupportsAccess(optionType, consumerType, variableAccessFlag));
                options.Add(new VariableTypeMenuOption(type, isCurrent ? currentType.ActualType : null, isCurrent, isCompatible));
            }

            return options;
        }

        /// <summary>Decides whether a type-menu entry is disabled, a checked current entry, a read-only view, or an actual type switch.</summary>
        internal static TypeMenuOptionState GetTypeMenuOptionState(VariableTypeMenuOption option, bool canSwitchType)
        {
            if (!option.IsCompatible)
            {
                return TypeMenuOptionState.Disabled;
            }
            if (option.IsCurrent)
            {
                return TypeMenuOptionState.Current;
            }
            return canSwitchType ? TypeMenuOptionState.Switchable : TypeMenuOptionState.ViewOnly;
        }

        /// <summary>Whether the field may change its authored constant storage type.</summary>
        internal static bool CanSwitchConstantType(VariableFieldBase variable)
            => variable is VariableField field && field.IsConstant;

        /// <summary>Collects legal variable candidates for the selector using the same directional conversion rule as the type menu.</summary>
        /// <remarks>The candidate's real CLR type and category are resolved before visibility and compatibility are checked.</remarks>
        internal static List<VariableData> GetCompatibleVariables(
            BehaviourTreeData tree,
            IReadOnlyList<VariableType> visibleTypes,
            Type consumerType,
            VariableAccessFlag variableAccessFlag,
            List<VariableData> allVariables)
        {
            List<VariableData> candidates = new();
            if (allVariables == null || visibleTypes == null)
            {
                return candidates;
            }

            VariableAccessFlag directions = NormalizeAccess(variableAccessFlag);
            bool canCompareConsumer = consumerType == null || IsSupportedAccessorType(consumerType);
            for (int i = 0; i < allVariables.Count; i++)
            {
                VariableData data = allVariables[i];
                Type candidateType = ResolveCandidateValueType(data, tree, directions);
                if (!IsSupportedAccessorType(candidateType))
                {
                    continue;
                }

                VariableType category = VariableTypeCatalog.Of(candidateType);
                if (!IsSelectableVariableCategory(category) || !ContainsType(visibleTypes, category))
                {
                    continue;
                }

                if (consumerType != null
                    && (!canCompareConsumer || !VariableTypeCatalog.SupportsAccess(candidateType, consumerType, variableAccessFlag)))
                {
                    continue;
                }

                candidates.Add(data);
            }

            return candidates;
        }

        /// <summary>Builds the selector entries, preserving an invalid binding as its own non-selectable current entry.</summary>
        /// <param name="boundUuid">The authored reference UUID; empty for an unbound field.</param>
        /// <param name="boundVariable">The variable currently bound in the tree, or null when the reference is missing.</param>
        /// <param name="canCreate">Whether a legal creation type exists; when false no executable Create entry is offered.</param>
        internal static VariableSelectionOptions BuildVariableSelectionOptions(
            BehaviourTreeData tree,
            UUID boundUuid,
            VariableData boundVariable,
            IReadOnlyList<VariableData> candidates,
            bool canCreate,
            string createLabel)
        {
            List<VariableSelectionOption> options = new();
            IReadOnlyList<VariableData> available = candidates ?? Array.Empty<VariableData>();
            bool isMissing = boundUuid != UUID.Empty && boundVariable == null;
            bool isExcluded = boundVariable != null && !ContainsVariable(available, boundUuid);
            bool hasInvalidCurrent = isMissing || isExcluded;
            int noneIndex;
            if (hasInvalidCurrent)
            {
                GUIContent invalidContent;
                if (isMissing)
                {
                    invalidContent = BuildInvalidSelectionContent(
                        "Missing",
                        boundUuid.Value,
                        "The bound variable no longer exists in this tree or global settings. The binding is preserved until you choose another variable, clear it, or recreate it.");
                }
                else
                {
                    string boundName = tree != null ? tree.GetVariableDescName(boundVariable) : boundVariable.name;
                    invalidContent = BuildInvalidSelectionContent(
                        boundName,
                        boundUuid.Value,
                        "This binding is hidden by this field's rules or cannot be used for the required access. It is preserved until you choose another variable, clear it, or recreate it.");
                }
                options.Add(new VariableSelectionOption(VariableSelectionKind.InvalidCurrent, boundVariable, invalidContent));
                noneIndex = 1;
            }
            else
            {
                noneIndex = 0;
            }

            options.Add(new VariableSelectionOption(VariableSelectionKind.None, null, new GUIContent(NONE_VARIABLE_NAME)));
            for (int i = 0; i < available.Count; i++)
            {
                VariableData candidate = available[i];
                string name = tree != null ? tree.GetVariableDescName(candidate) : candidate.name;
                options.Add(new VariableSelectionOption(VariableSelectionKind.Candidate, candidate, new GUIContent(name, candidate.UUID.Value)));
            }

            if (canCreate)
            {
                options.Add(new VariableSelectionOption(VariableSelectionKind.Create, null, new GUIContent(createLabel ?? "Create New…")));
            }

            int currentIndex;
            if (hasInvalidCurrent)
            {
                currentIndex = 0;
            }
            else if (boundVariable == null)
            {
                currentIndex = noneIndex;
            }
            else
            {
                currentIndex = noneIndex + 1 + IndexOfVariable(available, boundUuid);
            }

            return new VariableSelectionOptions(options, currentIndex);
        }

        /// <summary>Builds one preserved invalid binding entry: the short row label stays readable while the tooltip carries the identity and reason.</summary>
        private static GUIContent BuildInvalidSelectionContent(string text, string uuid, string reason)
        {
            Texture warning = EditorGUIUtility.IconContent("console.warnicon.sml").image;
            return new GUIContent(text, warning, $"{text}\nUUID: {uuid}\n{reason}");
        }

        private static LayerMask DrawLayerMask(Rect position, GUIContent label, LayerMask lm)
        {
            string[] layers = System.Linq.Enumerable.Range(0, 31)
                .Select(index => LayerMask.LayerToName(index))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToArray();
            return new LayerMask { value = EditorGUI.MaskField(position, label, lm.value, layers) };
        }

        /// <summary>
        /// Draw the variable field within a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="property">The serialized property representing the variable.</param>
        /// <returns>True if any value changes occurred.</returns> 
        public static bool DrawVariable(Rect position, GUIContent label, SerializedProperty property)
            => DrawVariable(position, label, property, null, null, null);

        /// <summary>
        /// Draw the variable field within a fixed position with explicit variable constraints.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="property">The serialized property representing the variable.</param>
        /// <param name="possibleTypes">Visible variable types, or null to resolve from the field metadata.</param>
        /// <param name="variableAccessFlag">Access constraint, or null to resolve from the field metadata.</param>
        /// <returns>True if the property value changes.</returns>
        public static bool DrawVariable(Rect position, GUIContent label, SerializedProperty property, IReadOnlyList<VariableType> possibleTypes, VariableAccessFlag? variableAccessFlag)
            => DrawVariable(position, label, property, possibleTypes, variableAccessFlag, null);

        /// <summary>Draws a variable field with a transient reflected object type override.</summary>
        public static bool DrawVariable(Rect position, GUIContent label, SerializedProperty property, IReadOnlyList<VariableType> possibleTypes, VariableAccessFlag? variableAccessFlag, Type consumerTypeOverride)
        {
            if (property == null)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Variable property is missing"));
                return false;
            }

            if (property.serializedObject.targetObject is not BehaviourTreeData tree || tree == null)
            {
                // error that tree is missing
                EditorGUI.LabelField(position, label, new GUIContent("Behaviour Tree Data is missing"));
                return false;
            }
            if (property.boxedValue is not VariableFieldBase variable)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Variable field is missing"));
                return false;
            }

            // from member info, try get contraint
            var memberInfo = property.GetAIMemberInfo();
            IReadOnlyList<VariableType> resolvedTypes = possibleTypes;
            VariableAccessFlag? resolvedAccessFlag = variableAccessFlag;
            if (memberInfo != null)
            {
                resolvedTypes ??= VariableFieldEditorMetadata.GetAllowedTypes(memberInfo);
                resolvedAccessFlag ??= VariableFieldEditorMetadata.GetAccessFlag(memberInfo);
            }
            resolvedAccessFlag ??= VariableAccessFlag.All;

            EditorGUI.BeginProperty(position, label, property);

            EditorGUI.BeginChangeCheck();
            DrawVariable(position, label, variable, tree, resolvedTypes, resolvedAccessFlag.Value, property, consumerTypeOverride);
            if (EditorGUI.EndChangeCheck())
            {
                property.serializedObject.Update();
                property.boxedValue = variable;
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                EditorGUI.EndProperty();
                return true;
            }
            EditorGUI.EndProperty();
            return false;
        }

        /// <summary>
        /// Draw the variable field within a fixed position.
        /// </summary>
        /// <param name="position">The position rectangle to draw within.</param>
        /// <param name="label">Label of the field.</param>
        /// <param name="variable">The variable instance.</param>
        /// <param name="tree">The behaviour tree data associated with the variable.</param>
        /// <param name="possibleTypes">Visible type constraint, null for no restraint.</param>
        /// <param name="variableAccessFlag">Access constraint for the variable.</param>
        /// <param name="sourceProperty">Optional serialized property used to safely resolve menu mutations later.</param>
        /// <returns>True if any value changes occurred.</returns>
        public static void DrawVariable(Rect position, GUIContent label, VariableFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None, SerializedProperty sourceProperty = null, Type consumerTypeOverride = null)
        {
            possibleTypes ??= ALL_VARIABLES;
            Rect row = GetRowRect(position);

            Type type = variable.GetType();
            if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(VariableReference<>)) || type == typeof(VariableReference))
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: false, sourceProperty, consumerTypeOverride);
            else if (variable is VariableValueFieldBase valueField && valueField.IsConstant)
                DrawVariableConstant(row, label, valueField, tree, possibleTypes, variableAccessFlag, sourceProperty, consumerTypeOverride);
            else
                DrawVariableSelection(row, label, variable, tree, possibleTypes, variableAccessFlag, allowConvertToConstant: true, sourceProperty, consumerTypeOverride);
        }

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="labelName">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static bool DrawVariable(string labelName, VariableFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            return DrawVariable(new GUIContent(labelName), variable, tree, possibleTypes, variableAccessFlag);
        }

        /// <summary>
        /// Draw the variable field
        /// </summary>
        /// <param name="label">name of the label</param>
        /// <param name="variable">the variable</param>
        /// <param name="tree">the behaviour tree data associate with</param>
        /// <param name="possibleTypes">type restraint, null for no restraint</param>
        public static bool DrawVariable(GUIContent label, VariableFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            float height = GetVariableHeight(variable, tree, possibleTypes, variableAccessFlag);
            Rect rect = EditorGUILayout.GetControlRect(true, height);

            EditorGUI.BeginChangeCheck();
            DrawVariable(rect, label, variable, tree, possibleTypes, variableAccessFlag);
            return EditorGUI.EndChangeCheck();
        }

        /// <summary>
        /// Draws a read-only summary of a variable field for runtime inspection.
        /// </summary>
        /// <remarks>
        /// This method intentionally does not use the authoring variable drawer. Runtime
        /// fields may carry a resolved runtime binding, and changing their authoring
        /// reference while drawing would invalidate that binding.
        /// </remarks>
        public static void DrawRuntimeSummary(GUIContent label, VariableFieldBase variable)
        {
            Rect row = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            EditorGUI.LabelField(row, label, new GUIContent(GetRuntimeSummary(variable)));
        }

        /// <summary>
        /// Gets the text shown by <see cref="DrawRuntimeSummary"/> without mutating the field.
        /// </summary>
        internal static string GetRuntimeSummary(VariableFieldBase variable)
        {
            if (variable == null)
            {
                return "null (Variable)";
            }

            if (variable.HasEditorReference)
            {
                if (!variable.HasReference || variable.RuntimeVariable == null)
                {
                    return $"Unresolved ({variable.UUID})";
                }

                RuntimeVariable runtimeVariable = variable.RuntimeVariable;
                return $"{runtimeVariable.Name} ({runtimeVariable.UUID}) = {GetRuntimeValueSummary(runtimeVariable)}";
            }

            if (variable is VariableValueFieldBase valueField && valueField.IsConstant)
            {
                return $"Constant = {GetConstantValueSummary(valueField)}";
            }

            return "No variable";
        }

        private static string GetConstantValueSummary(VariableValueFieldBase valueField)
        {
            try
            {
                return FormatSummaryValue(valueField.Value);
            }
            catch (Exception exception)
            {
                return $"<unavailable: {exception.GetType().Name}>";
            }
        }

        private static string GetRuntimeValueSummary(RuntimeVariable runtimeVariable)
        {
            try
            {
                return FormatSummaryValue(runtimeVariable.Value);
            }
            catch (Exception exception)
            {
                return $"<unavailable: {exception.GetType().Name}>";
            }
        }

        private static string FormatSummaryValue(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is UnityEngine.Object unityObject && unityObject == null)
            {
                return "null";
            }

            return value.ToString();
        }

        private static void DrawRowLabel(Rect rect, GUIContent label)
        {
            if (rect.width > 0f && label != null)
            {
                EditorGUI.LabelField(rect, label);
            }
        }

        /// <summary>
        /// Draw constant variable field
        /// </summary>
        private static void DrawVariableConstant(Rect row, GUIContent label, VariableValueFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes, VariableAccessFlag variableAccessFlag, SerializedProperty sourceProperty, Type consumerTypeOverride)
        {
            Type consumerType = TryResolveConsumerType(variable, consumerTypeOverride, out Type resolvedConsumerType) ? resolvedConsumerType : null;
            List<VariableData> candidates = GetCompatibleVariables(
                tree, possibleTypes, consumerType, variableAccessFlag, GetAllVariable(tree));
            VariableType? creationType = ResolveCreationType(variable, possibleTypes, consumerType, variableAccessFlag);
            VariableRowLayout layout = CalculateRowLayout(row, hasAction: true);

            DrawRowLabel(layout.LabelRect, label);
            DrawConstantEditor(layout.ValueRect, variable, consumerTypeOverride ?? (variable is IDynamicVariableField ? VariableUtility.GetType(variable.Type) : variable.DeclaredValueType));
            DrawTypeDropdown(layout.TypeRect, variable, possibleTypes, consumerType, variableAccessFlag, tree, sourceProperty);

            if (GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
            {
                GenericMenu menu = new();
                AddMutation(menu, tree, new GUIContent("Use Variable"), sourceProperty, variable, candidates.Count > 0, v => v.SetReference(candidates[0]));
                AddMutation(menu, tree, new GUIContent("Create Variable"), sourceProperty, variable, candidates.Count == 0 && creationType != null, v =>
                    CreateVariable(tree, v, creationType.Value));
                menu.ShowAsContext();
            }
        }

        /// <summary>Draws one constant editor and writes only when the control value actually changes.</summary>
        private static void DrawConstantEditor(Rect rect, VariableValueFieldBase variable, Type resolvedType)
        {
            GUIContent noLabel = GUIContent.none;
            switch (variable.Type)
            {
                case VariableType.Int:
                    {
                        int intVal = variable.IntValue;
                        if (resolvedType?.IsEnum == true)
                        {
                            Enum value = (Enum)Enum.Parse(resolvedType, intVal.ToString());
                            Enum newValue = Attribute.GetCustomAttribute(value.GetType(), typeof(FlagsAttribute)) == null
                                ? EditorGUI.EnumPopup(rect, noLabel, value)
                                : EditorGUI.EnumFlagsField(rect, noLabel, value);
                            if (!Equals(newValue, value))
                            {
                                variable.ForceSetConstantValue(Convert.ToInt32(newValue));
                            }
                        }
                        else if (resolvedType == typeof(uint))
                        {
                            int newValue = EditorGUI.IntField(rect, noLabel, intVal);
                            if (newValue != intVal)
                            {
                                variable.ForceSetConstantValue(newValue);
                            }
                        }
                        else if (resolvedType == typeof(LayerMask))
                        {
                            LayerMask oldMask = new() { value = intVal };
                            LayerMask newValue = DrawLayerMask(rect, noLabel, oldMask);
                            if (newValue.value != oldMask.value)
                            {
                                variable.ForceSetConstantValue(newValue.value);
                            }
                        }
                        else
                        {
                            int newValue = EditorGUI.IntField(rect, noLabel, intVal);
                            if (newValue != intVal)
                            {
                                variable.ForceSetConstantValue(newValue);
                            }
                        }
                        break;
                    }
                case VariableType.String:
                    {
                        string oldValue = variable.StringValue;
                        string newValue = EditorGUI.TextField(rect, noLabel, oldValue);
                        if (!string.Equals(newValue, oldValue, StringComparison.Ordinal))
                        {
                            variable.ForceSetConstantValue(newValue);
                        }
                        break;
                    }
                case VariableType.Float:
                    {
                        float oldValue = variable.FloatValue;
                        float newValue = EditorGUI.FloatField(rect, noLabel, oldValue);
                        if (!newValue.Equals(oldValue))
                        {
                            variable.ForceSetConstantValue(newValue);
                        }
                        break;
                    }
                case VariableType.Bool:
                    {
                        bool oldValue = variable.BoolValue;
                        bool newValue = EditorGUI.Toggle(rect, noLabel, oldValue);
                        if (newValue != oldValue)
                        {
                            variable.ForceSetConstantValue(newValue);
                        }
                        break;
                    }
                case VariableType.Vector2:
                    using (new WideModeScope(true))
                    {
                        Vector2 oldValue = variable.Vector2Value;
                        Vector2 newValue = EditorGUI.Vector2Field(rect, noLabel, oldValue);
                        if (!newValue.Equals(oldValue))
                        {
                            variable.ForceSetConstantValue(newValue);
                        }
                    }
                    break;
                case VariableType.Vector3:
                    using (new WideModeScope(true))
                    {
                        Vector3 oldValue = variable.Vector3Value;
                        Vector3 newValue = EditorGUI.Vector3Field(rect, noLabel, oldValue);
                        if (!newValue.Equals(oldValue))
                        {
                            variable.ForceSetConstantValue(newValue);
                        }
                    }
                    break;
                case VariableType.Vector4:
                    {
                        using (new WideModeScope(true))
                        {
                            if (resolvedType == typeof(Color))
                            {
                                Color oldValue = variable.ColorValue;
                                Color newValue = EditorGUI.ColorField(rect, noLabel, oldValue);
                                if (!newValue.Equals(oldValue))
                                {
                                    variable.ForceSetConstantValue((Vector4)newValue);
                                }
                            }
                            else
                            {
                                Vector4 oldValue = variable.Vector4Value;
                                Vector4 newValue = EditorGUI.Vector4Field(rect, noLabel, oldValue);
                                if (!newValue.Equals(oldValue))
                                {
                                    variable.ForceSetConstantValue(newValue);
                                }
                            }
                        }
                        break;
                    }
                case VariableType.UnityObject:
                    {
                        UnityEngine.Object asset = variable.UnityObjectValue;
                        Type objectFieldType = resolvedType != null && typeof(UnityEngine.Object).IsAssignableFrom(resolvedType)
                            ? resolvedType
                            : typeof(UnityEngine.Object);
                        UnityEngine.Object newAsset = EditorGUI.ObjectField(rect, noLabel, asset, objectFieldType, false);
                        if (!ReferenceEquals(newAsset, asset))
                        {
                            variable.ForceSetConstantValue(newAsset);
                        }
                        break;
                    }
                default:
                    EditorGUI.LabelField(rect, new GUIContent($"Cannot set a constant value for {variable.Type}"));
                    break;
            }
        }

        /// <summary>Draws the current-type dropdown and builds its compatibility menu only when opened.</summary>
        private static void DrawTypeDropdown(
            Rect rect,
            VariableFieldBase variable,
            IReadOnlyList<VariableType> visibleTypes,
            Type consumerType,
            VariableAccessFlag variableAccessFlag,
            BehaviourTreeData tree,
            SerializedProperty sourceProperty)
        {
            if (rect.width <= 0f)
            {
                return;
            }

            CurrentVariableType currentType = ResolveCurrentVariableType(tree, variable);
            string label = currentType.ActualType == null ? "Missing" : GetTypeLabel(currentType.Category);
            string tooltip = BuildCurrentTypeTooltip(variable, currentType, visibleTypes, consumerType, variableAccessFlag);
            GUIContent content = new(TruncateText(label, EditorStyles.popup, rect.width), tooltip);
            if (!GUI.Button(rect, content, EditorStyles.popup))
            {
                return;
            }

            IReadOnlyList<VariableTypeMenuOption> options = BuildTypeMenuOptions(
                variable, visibleTypes, consumerType, variableAccessFlag, currentType);
            bool canSwitchType = CanSwitchConstantType(variable);
            GenericMenu menu = new();
            for (int i = 0; i < options.Count; i++)
            {
                AddTypeMenuOption(menu, tree, sourceProperty, variable, options[i], consumerType, variableAccessFlag, canSwitchType);
            }

            if (options.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No visible types"));
            }

            menu.ShowAsContext();
        }

        /// <summary>Adds one type entry with the correct check, disabled, or switchable state.</summary>
        private static void AddTypeMenuOption(
            GenericMenu menu,
            BehaviourTreeData tree,
            SerializedProperty sourceProperty,
            VariableFieldBase variable,
            VariableTypeMenuOption option,
            Type consumerType,
            VariableAccessFlag variableAccessFlag,
            bool canSwitchType)
        {
            string label = GetTypeLabel(option.Type);
            string description = BuildTypeOptionTooltip(option, consumerType, variableAccessFlag);
            GUIContent content = new(label, description);
            switch (GetTypeMenuOptionState(option, canSwitchType))
            {
                case TypeMenuOptionState.Disabled:
                    menu.AddDisabledItem(content, option.IsCurrent);
                    break;
                case TypeMenuOptionState.Current:
                case TypeMenuOptionState.ViewOnly:
                    // Read-only fields remain inspectable but cannot rewrite their authored type.
                    menu.AddItem(content, option.IsCurrent, NoOperation);
                    break;
                case TypeMenuOptionState.Switchable:
                    VariableType selectedType = option.Type;
                    AddMutation(menu, tree, content, sourceProperty, variable, true,
                        v => ((VariableField)v).ForceSetConstantType(selectedType));
                    break;
            }
        }

        private static void NoOperation() { }

        private static string GetTypeLabel(VariableType type)
            => type == VariableType.UnityObject ? "Object" : type.ToString();

        private static string TruncateText(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f)
            {
                return string.Empty;
            }

            if (style.CalcSize(new GUIContent(text)).x <= width)
            {
                return text;
            }

            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                string candidate = text.Substring(0, middle) + TruncationSuffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low == 0 ? TruncationSuffix : text.Substring(0, low) + TruncationSuffix;
        }

        /// <summary>Describes the current value type, its state, and the conversion the field actually performs for its access directions.</summary>
        /// <param name="variable">The field being drawn.</param>
        /// <param name="currentType">The actual value type resolved for this draw pass.</param>
        /// <param name="visibleTypes">The visible type constraint, or null when unconstrained.</param>
        /// <param name="consumerType">The CLR type the field consumes, or null when the field has no explicit consumer.</param>
        /// <param name="variableAccessFlag">The field's required read/write access.</param>
        /// <returns>The type button tooltip.</returns>
        internal static string BuildCurrentTypeTooltip(
            VariableFieldBase variable,
            CurrentVariableType currentType,
            IReadOnlyList<VariableType> visibleTypes,
            Type consumerType,
            VariableAccessFlag variableAccessFlag)
        {
            List<string> lines = new();
            if (currentType.ActualType == null)
            {
                lines.Add("Missing or unsupported type.");
            }
            else
            {
                lines.Add(currentType.ActualType.FullName ?? currentType.ActualType.Name);
                bool hiddenGenericConstant = currentType.Category == VariableType.Generic && CanSwitchConstantType(variable);
                if (hiddenGenericConstant)
                {
                    lines.Add("Hidden: dynamic constants have no Generic editor.");
                }
                else if (visibleTypes != null && !ContainsType(visibleTypes, currentType.Category))
                {
                    lines.Add("Hidden by constraints.");
                }

                if (consumerType != null)
                {
                    AppendConversionLines(lines, currentType.ActualType, consumerType, variableAccessFlag);
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>Builds the optional short menu tooltip for one candidate type using the same direction lines as the type button.</summary>
        private static string BuildTypeOptionTooltip(
            VariableTypeMenuOption option,
            Type consumerType,
            VariableAccessFlag variableAccessFlag)
        {
            if (consumerType == null)
            {
                return string.Empty;
            }

            Type optionType = option.IsCurrent && option.ActualType != null
                ? option.ActualType
                : VariableUtility.GetType(option.Type);
            if (!IsSupportedAccessorType(optionType))
            {
                return string.Empty;
            }

            List<string> lines = new();
            AppendConversionLines(lines, optionType, consumerType, variableAccessFlag);
            return string.Join("\n", lines);
        }

        /// <summary>Appends required conversion directions when the types differ, using the matched definition's own explanation.</summary>
        private static void AppendConversionLines(
            List<string> lines,
            Type variableType,
            Type consumerType,
            VariableAccessFlag variableAccessFlag)
        {
            if (variableType == consumerType)
            {
                return;
            }

            VariableAccessFlag directions = NormalizeAccess(variableAccessFlag);
            if ((directions & VariableAccessFlag.Read) != 0)
            {
                lines.Add(BuildConversionLine("Read", variableType, consumerType));
            }
            if ((directions & VariableAccessFlag.Write) != 0)
            {
                lines.Add(BuildConversionLine("Write", consumerType, variableType));
            }
        }

        /// <summary>Builds one direction line from the definition that binds the pair, or marks the direction unsupported.</summary>
        private static string BuildConversionLine(string direction, Type source, Type target)
        {
            VariableType sourceCategory = VariableTypeCatalog.Of(source);
            VariableType targetCategory = VariableTypeCatalog.Of(target);
            string sourceLabel = VariableUtility.GetType(sourceCategory) == source ? GetTypeLabel(sourceCategory) : source.Name;
            string targetLabel = VariableUtility.GetType(targetCategory) == target ? GetTypeLabel(targetCategory) : target.Name;
            if (!IsSupportedAccessorType(source) || !IsSupportedAccessorType(target))
            {
                return $"{direction}: {sourceLabel} → {targetLabel} — Unsupported.";
            }

            ConversionDefinition definition = ConversionRegistry.Find(source, target);
            string description = definition != null ? definition.Describe(source, target) : "Unsupported.";
            return $"{direction}: {sourceLabel} → {targetLabel} — {description}";
        }

        private static void DrawVariableSelection(Rect row, GUIContent label, VariableFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes, VariableAccessFlag variableAccessFlag, bool allowConvertToConstant, SerializedProperty sourceProperty, Type consumerTypeOverride)
        {
            Type consumerType = TryResolveConsumerType(variable, consumerTypeOverride, out Type resolvedConsumerType) ? resolvedConsumerType : null;
            List<VariableData> allVariable = GetAllVariable(tree);
            VariableData boundVariable = variable.HasEditorReference
                ? allVariable.Find(v => v.UUID == variable.UUID)
                : null;
            var member = sourceProperty?.GetAIMemberInfo();
            bool requiresTimer = member?.Name == "timer" && (member.DeclaringType == typeof(Nodes.Cooldown) || member.DeclaringType == typeof(Nodes.Throttle));
            bool excludesTimer = member?.Name == "updatingVariable" && member.DeclaringType == typeof(Nodes.Countdown);
            if (requiresTimer) allVariable.RemoveAll(v => !v.IsTimer || v.IsStatic || v.IsGlobal || v.IsScript);
            if (excludesTimer) allVariable.RemoveAll(v => !IsCountdownCandidate(v, tree));
            string createLabel = requiresTimer ? "Create Timer…" : "Create New…";

            List<VariableData> candidates = GetCompatibleVariables(
                tree, possibleTypes, consumerType, variableAccessFlag, allVariable);
            VariableType? creationType;
            if (requiresTimer)
            {
                creationType = IsCreationTypeCompatible(VariableType.Float, possibleTypes, consumerType, variableAccessFlag)
                    ? VariableType.Float
                    : null;
            }
            else
            {
                creationType = ResolveCreationType(variable, possibleTypes, consumerType, variableAccessFlag);
            }

            VariableSelectionOptions selection = BuildVariableSelectionOptions(
                tree,
                variable.HasEditorReference ? variable.UUID : UUID.Empty,
                boundVariable,
                candidates,
                creationType != null,
                createLabel);
            bool hasInvalidBinding = selection.Options.Count > 0 && selection.Options[0].Kind == VariableSelectionKind.InvalidCurrent;
            bool hasValidVariable = candidates.Count > 0;
            bool hasAction = (allowConvertToConstant && variable.HasEditorReference)
                || !hasValidVariable
                || hasInvalidBinding;
            VariableRowLayout layout = CalculateRowLayout(row, hasAction);

            DrawRowLabel(layout.LabelRect, label);
            if (layout.ValueRect.width > 0f && selection.Options.Count > 0)
            {
                GUIContent[] contents = new GUIContent[selection.Options.Count];
                for (int i = 0; i < contents.Length; i++)
                {
                    contents[i] = selection.Options[i].Content;
                }

                int selectedIndex = EditorGUI.Popup(layout.ValueRect, selection.CurrentIndex, contents);
                if (selectedIndex >= 0 && selectedIndex < selection.Options.Count && selectedIndex != selection.CurrentIndex)
                {
                    VariableSelectionOption selected = selection.Options[selectedIndex];
                    switch (selected.Kind)
                    {
                        case VariableSelectionKind.None:
                            variable.SetReference(null);
                            break;
                        case VariableSelectionKind.Candidate:
                            VariableData candidate = candidates.Find(v => v.UUID == selected.Uuid);
                            if (candidate != null)
                            {
                                variable.SetReference(candidate);
                            }
                            break;
                        case VariableSelectionKind.Create:
                            if (creationType != null)
                            {
                                Undo.RecordObject(tree, requiresTimer ? "Create Timer" : "Create Variable");
                                CreateSelectedVariable(tree, variable, creationType.Value, requiresTimer);
                                EditorUtility.SetDirty(tree);
                            }
                            break;
                    }
                }
            }

            DrawTypeDropdown(layout.TypeRect, variable, possibleTypes, consumerType, variableAccessFlag, tree, sourceProperty);

            if (layout.HasOverflow && GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
            {
                GenericMenu menu = new();
                if (allowConvertToConstant && variable.HasEditorReference)
                {
                    AddMutation(menu, tree, new GUIContent("Set Constant"), sourceProperty, variable, true, v => v.SetReference(null));
                }
                if (!hasValidVariable && !hasInvalidBinding)
                {
                    AddMutation(menu, tree, new GUIContent(requiresTimer ? "Create Timer…" : "Create Variable"), sourceProperty, variable, creationType != null, CreateMatchingVariable);
                }
                if (hasInvalidBinding)
                {
                    AddMutation(menu, tree, new GUIContent("Recreate"), sourceProperty, variable, creationType != null, CreateMatchingVariable);
                    AddMutation(menu, tree, new GUIContent("Clear"), sourceProperty, variable, true, v => v.SetReference(null));
                }
                menu.ShowAsContext();
            }

            // Source constraints belong to these authored node fields, not to ordinary Float references.
            void CreateMatchingVariable(VariableFieldBase field)
            {
                if (creationType == null)
                {
                    return;
                }

                CreateSelectedVariable(tree, field, creationType.Value, requiresTimer);
            }
        }

        /// <summary>Creates the already-validated variable category for a selector action.</summary>
        private static void CreateSelectedVariable(BehaviourTreeData tree, VariableFieldBase field, VariableType type, bool timer)
        {
            if (timer)
            {
                VariableData timerVariable = tree.CreateNewVariable(VariableType.Float, tree.GenerateNewVariableName("timer"));
                VariableAuthoring.ConfigureTimer(timerVariable);
                field.SetReference(timerVariable);
                return;
            }

            CreateVariable(tree, field, type);
        }

        /// <summary>Registers a menu mutation with a fresh serialized-property lookup.</summary>
        private static void AddMutation(GenericMenu menu, BehaviourTreeData tree, GUIContent content, SerializedProperty source, VariableFieldBase fallback, bool enabled, Action<VariableFieldBase> mutation)
        {
            if (!enabled)
            {
                menu.AddDisabledItem(content);
                return;
            }

            UnityEngine.Object target = source?.serializedObject.targetObject;
            string propertyPath = source?.propertyPath;
            menu.AddItem(content, false, () => ApplyMutation(tree, target, propertyPath, fallback, mutation));
        }

        /// <summary>Applies one menu mutation in a single undo and dirty transaction.</summary>
        private static void ApplyMutation(BehaviourTreeData tree, UnityEngine.Object target, string propertyPath, VariableFieldBase fallback, Action<VariableFieldBase> mutation)
        {
            UnityEngine.Object undoTarget = target != null ? target : tree;
            if (undoTarget == null) return;
            if (target == null || string.IsNullOrEmpty(propertyPath))
            {
                Undo.RecordObject(undoTarget, "Edit Variable");
                mutation(fallback);
                EditorUtility.SetDirty(undoTarget);
                return;
            }
            SerializedObject serializedObject = new(target);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property?.boxedValue is not VariableFieldBase current) return;
            Undo.RecordObject(target, "Edit Variable");
            if (tree != null && tree != target) Undo.RecordObject(tree, "Edit Variable");
            mutation(current);
            property.boxedValue = current;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (tree != null && tree != target) EditorUtility.SetDirty(tree);
        }



        #region Save

        private static VariableData FindVariable(BehaviourTreeData tree, UUID uuid)
        {
            if (uuid == UUID.Empty)
            {
                return null;
            }

            if (tree != null)
            {
                VariableData data = tree.GetVariable(uuid);
                if (data != null)
                {
                    return data;
                }
            }

            return AISetting.Instance.globalVariables.FirstOrDefault(v => v.UUID == uuid);
        }

        private static Type GetTargetScriptClass(BehaviourTreeData tree)
        {
            Type targetClass = tree != null && tree.targetScript ? tree.targetScript.GetClass() : null;
            return IsSupportedAccessorType(targetClass) ? targetClass : null;
        }

        private static MemberInfo FindScriptMember(Type targetClass, string path)
        {
            return targetClass == null || string.IsNullOrEmpty(path)
                ? null
                : targetClass.GetMember(path).FirstOrDefault();
        }

        private static Type ResolveCandidateValueType(VariableData data, BehaviourTreeData tree, VariableAccessFlag directions)
        {
            if (data == null)
            {
                return null;
            }

            if (data.UUID == VariableData.targetScript || !data.IsScript)
            {
                return ResolveVariableActualType(data, tree);
            }

            Type targetClass = GetTargetScriptClass(tree);
            MemberInfo member = FindScriptMember(targetClass, data.Path);
            if (!VariableMemberInfo.TryResolve(member, out VariableMemberInfo resolved)) return null;
            if ((directions & VariableAccessFlag.Read) != 0 && !resolved.CanRead) return null;
            if ((directions & VariableAccessFlag.Write) != 0 && !resolved.CanWrite) return null;
            return resolved.Type;
        }

        private static bool IsSelectableVariableCategory(VariableType type)
            => type is not VariableType.Node and not VariableType.Invalid;

        private static VariableAccessFlag NormalizeAccess(VariableAccessFlag access)
            => access == VariableAccessFlag.None ? VariableAccessFlag.All : access;

        /// <summary>Resolves a compatible category for a newly authored variable, or null when no category can create a compatible variable.</summary>
        internal static VariableType? ResolveCreationType(VariableFieldBase variable, IReadOnlyList<VariableType> visibleTypes, Type consumerType, VariableAccessFlag variableAccessFlag)
        {
            if (variable == null || visibleTypes == null)
            {
                return null;
            }

            if (IsCreationTypeCompatible(variable.Type, visibleTypes, consumerType, variableAccessFlag))
            {
                return variable.Type;
            }

            for (int i = 0; i < visibleTypes.Count; i++)
            {
                VariableType candidate = visibleTypes[i];
                if (IsCreationTypeCompatible(candidate, visibleTypes, consumerType, variableAccessFlag))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>Checks the actual type <see cref="BehaviourTreeData.CreateNewVariable"/> would store for a category against the consumer.</summary>
        /// <remarks>The created variable uses the canonical category type, so a field's generic CLR argument must not stand in for a different stored type.</remarks>
        internal static bool IsCreationTypeCompatible(
            VariableType type,
            IReadOnlyList<VariableType> visibleTypes,
            Type consumerType,
            VariableAccessFlag variableAccessFlag)
        {
            if (!IsSelectableVariableCategory(type) || visibleTypes == null || !ContainsType(visibleTypes, type))
            {
                return false;
            }
            if (consumerType == null)
            {
                return true;
            }
            if (!IsSupportedAccessorType(consumerType))
            {
                return false;
            }

            Type createdType = VariableUtility.GetType(type);
            return IsSupportedAccessorType(createdType)
                && VariableTypeCatalog.SupportsAccess(createdType, consumerType, variableAccessFlag);
        }

        private static bool ContainsVariable(IReadOnlyList<VariableData> variables, UUID uuid)
        {
            return IndexOfVariable(variables, uuid) >= 0;
        }

        private static int IndexOfVariable(IReadOnlyList<VariableData> variables, UUID uuid)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                VariableData variable = variables[i];
                if (variable != null && variable.UUID == uuid)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Mirrors Countdown's runtime Float read/write boundary for editor selection.</summary>
        private static bool IsCountdownCandidate(VariableData variable, BehaviourTreeData tree)
        {
            Type actualType = ResolveVariableActualType(variable, tree);
            if (actualType == null || VariableTypeCatalog.Of(actualType) != VariableType.Float)
            {
                return false;
            }

            if (variable.IsScript)
            {
                MemberInfo member = FindScriptMember(GetTargetScriptClass(tree), variable.Path);
                return VariableMemberInfo.TryResolve(member, out VariableMemberInfo resolved) && resolved.CanRead && resolved.CanWrite;
            }

            return !variable.IsTimer;
        }

        private static void CreateVariable(BehaviourTreeData tree, VariableFieldBase variable, VariableType type, string name = null)
        {
            string newVarName = name ?? tree.GenerateNewVariableName(type.ToString());
            variable.SetReference(tree.CreateNewVariable(type, newVarName));
        }

        #endregion




        private static List<VariableData> GetAllVariable(BehaviourTreeData tree)
        {
            if (tree == null)
            {
                Debug.Log("Missing Tree when achiving variables");
                return new List<VariableData>();
            }

            List<VariableData> enumerable = tree.EditorVariables.Union(AISetting.Instance.globalVariables).ToList();
            enumerable.Add(GameObjectVariable);
            enumerable.Add(TransformVariable);
            enumerable.Add(VariableData.TargetScriptVariable);
            return enumerable;
        }

        /// <summary>
        /// Get the height required to draw the variable field with fixed positioning.
        /// </summary>
        /// <param name="variable">The variable instance.</param>
        /// <param name="tree">The behaviour tree data associated with the variable.</param>
        /// <param name="possibleTypes">Type constraint, null for no restraint.</param>
        /// <param name="variableAccessFlag">Access constraint for the variable.</param>
        /// <returns>The required height for drawing the field.</returns>
        public static float GetVariableHeight(VariableFieldBase variable, BehaviourTreeData tree, IReadOnlyList<VariableType> possibleTypes = null, VariableAccessFlag variableAccessFlag = VariableAccessFlag.None)
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        private static bool ContainsType(IReadOnlyList<VariableType> types, VariableType value)
        {
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == value) return true;
            }

            return false;
        }
    }
}
