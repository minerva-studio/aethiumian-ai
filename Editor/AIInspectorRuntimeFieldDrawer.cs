using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Draws runtime node fields for AIInspector without depending on shared editor field drawers.
    /// </summary>
    internal static class AIInspectorRuntimeFieldDrawer
    {
        internal enum FieldDrawKind
        {
            Null,
            Variable,
            NodeReference,
            Uuid,
            GenericEditable,
            ReadOnlyUnsupported
        }

        public static bool DrawField(BehaviourTree activeTree, GUIContent label, object value, Type declaredType, out object newValue)
        {
            newValue = value;
            FieldDrawKind kind = ResolveDrawKind(value, declaredType);

            switch (kind)
            {
                case FieldDrawKind.Variable:
                    DrawVariable(activeTree, label, value as VariableBase);
                    return false;
                case FieldDrawKind.NodeReference:
                    DrawNodeReference(label, value as INodeReference);
                    return false;
                case FieldDrawKind.Uuid:
                    DrawUuid(label, value is UUID uuid ? uuid : UUID.Empty);
                    return false;
                case FieldDrawKind.GenericEditable:
                    return DrawGenericEditable(label, value, declaredType, out newValue);
                case FieldDrawKind.Null:
                    EditorGUILayout.LabelField(label, new GUIContent($"null ({GetTypeName(declaredType)})"));
                    return false;
                default:
                    EditorGUILayout.LabelField(label, new GUIContent($"({GetTypeName(declaredType)})"));
                    return false;
            }
        }

        internal static FieldDrawKind ResolveDrawKind(object value, Type declaredType)
        {
            if (value is VariableBase)
            {
                return FieldDrawKind.Variable;
            }

            if (value is INodeReference)
            {
                return FieldDrawKind.NodeReference;
            }

            if (value is UUID)
            {
                return FieldDrawKind.Uuid;
            }

            Type effectiveType = declaredType ?? value?.GetType();
            if (effectiveType == null)
            {
                return FieldDrawKind.Null;
            }

            if (typeof(Delegate).IsAssignableFrom(effectiveType) ||
                effectiveType.IsInterface ||
                IsUnsupportedAbstractType(effectiveType))
            {
                return FieldDrawKind.ReadOnlyUnsupported;
            }

            if (value == null)
            {
                return FieldDrawKind.Null;
            }

            return IsGenericEditableType(effectiveType, value)
                ? FieldDrawKind.GenericEditable
                : FieldDrawKind.ReadOnlyUnsupported;
        }

        private static void DrawVariable(BehaviourTree activeTree, GUIContent label, VariableBase variable)
        {
            if (variable == null)
            {
                EditorGUILayout.LabelField(label, "null (Variable)");
                return;
            }

            if (activeTree?.Prototype)
            {
                VariableFieldDrawers.DrawVariable("[Var] " + label.text, variable, activeTree.Prototype);
                return;
            }

            EditorGUILayout.LabelField(label, new GUIContent($"({variable.GetType().Name})"));
        }

        private static void DrawNodeReference(GUIContent label, INodeReference nodeReference)
        {
            TreeNode referencedNode = nodeReference?.Node;
            if (referencedNode != null)
            {
                EditorGUILayout.LabelField(label, $"Node {referencedNode.name} ({referencedNode.uuid})");
                return;
            }

            EditorGUILayout.LabelField(label, "Node (null)");
        }

        private static void DrawUuid(GUIContent label, UUID uuid)
        {
            EditorGUILayout.LabelField(label, uuid.Value);
        }

        private static bool DrawGenericEditable(GUIContent label, object value, Type declaredType, out object newValue)
        {
            if (value is IList list)
            {
                DrawListSummary(label, list, declaredType);
                newValue = value;
                return false;
            }

            EditorGUI.BeginChangeCheck();
            newValue = DrawKnownField(label, value, declaredType);
            bool guiChanged = EditorGUI.EndChangeCheck();

            return guiChanged && !Equals(value, newValue);
        }

        private static object DrawKnownField(GUIContent label, object value, Type declaredType)
        {
            if (declaredType == typeof(int) || value is int)
            {
                return EditorGUILayout.IntField(label, value is int intValue ? intValue : default);
            }

            if (declaredType == typeof(long) || value is long)
            {
                return EditorGUILayout.LongField(label, value is long longValue ? longValue : default);
            }

            if (declaredType == typeof(float) || value is float)
            {
                return EditorGUILayout.FloatField(label, value is float floatValue ? floatValue : default);
            }

            if (declaredType == typeof(double) || value is double)
            {
                return EditorGUILayout.DoubleField(label, value is double doubleValue ? doubleValue : default);
            }

            if (declaredType == typeof(bool) || value is bool)
            {
                return EditorGUILayout.Toggle(label, value is bool boolValue && boolValue);
            }

            if (declaredType == typeof(string))
            {
                return EditorGUILayout.TextField(label, value as string ?? string.Empty);
            }

            if (value is Enum enumValue)
            {
                return declaredType != null && Attribute.IsDefined(declaredType, typeof(FlagsAttribute))
                    ? EditorGUILayout.EnumFlagsField(label, enumValue)
                    : EditorGUILayout.EnumPopup(label, enumValue);
            }

            if (declaredType == typeof(Vector2) || value is Vector2)
            {
                return EditorGUILayout.Vector2Field(label, value is Vector2 vector ? vector : default);
            }

            if (declaredType == typeof(Vector2Int) || value is Vector2Int)
            {
                return EditorGUILayout.Vector2IntField(label, value is Vector2Int vector ? vector : default);
            }

            if (declaredType == typeof(Vector3) || value is Vector3)
            {
                return EditorGUILayout.Vector3Field(label, value is Vector3 vector ? vector : default);
            }

            if (declaredType == typeof(Vector3Int) || value is Vector3Int)
            {
                return EditorGUILayout.Vector3IntField(label, value is Vector3Int vector ? vector : default);
            }

            if (declaredType == typeof(Vector4) || value is Vector4)
            {
                return EditorGUILayout.Vector4Field(label, value is Vector4 vector ? vector : default);
            }

            if (declaredType == typeof(Color) || value is Color)
            {
                return EditorGUILayout.ColorField(label, value is Color color ? color : default);
            }

            if (declaredType == typeof(Rect) || value is Rect)
            {
                return EditorGUILayout.RectField(label, value is Rect rect ? rect : default);
            }

            if (declaredType == typeof(RectInt) || value is RectInt)
            {
                return EditorGUILayout.RectIntField(label, value is RectInt rect ? rect : default);
            }

            if (declaredType == typeof(Bounds) || value is Bounds)
            {
                return EditorGUILayout.BoundsField(label, value is Bounds bounds ? bounds : default);
            }

            if (declaredType == typeof(BoundsInt) || value is BoundsInt)
            {
                return EditorGUILayout.BoundsIntField(label, value is BoundsInt bounds ? bounds : default);
            }

            if (declaredType == typeof(LayerMask) || value is LayerMask)
            {
                return DrawLayerMask(label, value is LayerMask layerMask ? layerMask : default);
            }

            if (declaredType == typeof(Gradient))
            {
                return EditorGUILayout.GradientField(label, value as Gradient);
            }

            if (declaredType != null && IsUnityObjectType(declaredType))
            {
                return EditorGUILayout.ObjectField(label, value as UnityEngine.Object, declaredType, true);
            }

            EditorGUILayout.LabelField(label, new GUIContent($"({GetTypeName(declaredType)})"));
            return value;
        }

        private static void DrawListSummary(GUIContent label, IList list, Type declaredType)
        {
            string typeName = GetTypeName(declaredType);
            int count = list?.Count ?? 0;
            EditorGUILayout.LabelField(label, new GUIContent($"{typeName} ({count} items)"));
        }

        private static LayerMask DrawLayerMask(GUIContent label, LayerMask layerMask)
        {
            string[] layerNames = new string[32];
            for (int i = 0; i < layerNames.Length; i++)
            {
                layerNames[i] = LayerMask.LayerToName(i);
            }

            return new LayerMask { value = EditorGUILayout.MaskField(label, layerMask.value, layerNames) };
        }

        private static bool IsGenericEditableType(Type type, object value)
        {
            return type == typeof(string) ||
                   type.IsEnum ||
                   IsUnityObjectType(type) ||
                   IsKnownEditableValueType(type) ||
                   value is IList;
        }

        private static bool IsKnownEditableValueType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(bool) ||
                   type == typeof(Vector2) ||
                   type == typeof(Vector2Int) ||
                   type == typeof(Vector3) ||
                   type == typeof(Vector3Int) ||
                   type == typeof(Vector4) ||
                   type == typeof(Color) ||
                   type == typeof(Rect) ||
                   type == typeof(RectInt) ||
                   type == typeof(Bounds) ||
                   type == typeof(BoundsInt) ||
                   type == typeof(LayerMask) ||
                   type == typeof(Gradient);
        }

        private static bool IsUnsupportedAbstractType(Type type)
        {
            return type.IsAbstract && !IsUnityObjectType(type);
        }

        private static bool IsUnityObjectType(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private static string GetTypeName(Type type)
        {
            return type != null ? type.Name : "Unknown";
        }
    }
}
