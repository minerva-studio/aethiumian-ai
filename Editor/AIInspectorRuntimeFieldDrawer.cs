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
    /// Draws runtime node fields for AIInspector without expanding the global EditorFieldDrawers surface.
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
            EditorGUI.BeginChangeCheck();
            newValue = global::Minerva.Module.Editor.EditorFieldDrawers.DrawField(label, value, declaredType);
            bool guiChanged = EditorGUI.EndChangeCheck();

            // Lists are mutated by the drawer itself, so callers do not need to replace the field value.
            if (value is IList)
            {
                return false;
            }

            return guiChanged && !Equals(value, newValue);
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
