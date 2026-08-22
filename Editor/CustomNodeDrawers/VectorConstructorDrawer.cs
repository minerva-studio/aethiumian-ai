using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>Draws CreateVector nodes while restricting lane selectors to their source shape.</summary>
    internal abstract class VectorConstructorDrawerBase : NodeDrawerBase
    {
        private const string LaneSuffix = "Lane";
        private const string VectorResultName = "vector";
        private static readonly string[] SourceNames = { "x", "y", "z", "w" };
        private static readonly GUIContent[] LaneLabels =
        {
            new("X"),
            new("Y"),
            new("Z"),
            new("W"),
        };

        /// <summary>Gets the number of source lanes exposed by the concrete CreateVector node.</summary>
        protected abstract int SourceCount { get; }

        /// <summary>Draws the fixed CreateVector field layout without mutating a property iterator.</summary>
        public override void Draw()
        {
            NormalizeLanes(SourceCount);
            string[] fieldOrder = GetFieldOrder(SourceCount);
            for (int i = 0; i < fieldOrder.Length; i++)
            {
                string fieldName = fieldOrder[i];
                if (TryGetLaneSource(fieldName, out string sourceName))
                {
                    DrawLaneForSource(sourceName, fieldName);
                }
                else
                {
                    DrawPropertyRow(fieldName);
                }
            }
        }

        /// <summary>Normalizes all hidden or out-of-range lanes before the layout pass starts.</summary>
        private void NormalizeLanes(int sourceCount)
        {
            bool changed = false;
            for (int i = 0; i < sourceCount; i++)
            {
                string sourceName = SourceNames[i];
                SerializedProperty sourceProperty = property.FindPropertyRelative(sourceName);
                SerializedProperty laneProperty = property.FindPropertyRelative(sourceName + LaneSuffix);
                if (laneProperty == null)
                {
                    continue;
                }

                int componentCount = GetLaneCount(sourceProperty?.GetAIValue() as VariableFieldBase);
                if (!NormalizeLaneIndex(laneProperty.enumValueIndex, componentCount, out int normalizedIndex))
                {
                    continue;
                }

                laneProperty.enumValueIndex = normalizedIndex;
                changed = true;
            }

            if (changed && property.serializedObject.hasModifiedProperties)
            {
                // Apply once before the fixed layout pass. Updating between individual
                // rows is what invalidated the old iterator-based drawer.
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }
        }

        /// <summary>Returns the serialized field order used by a CreateVector node.</summary>
        internal static string[] GetFieldOrder(int sourceCount)
        {
            sourceCount = Math.Max(0, Math.Min(4, sourceCount));
            string[] fieldOrder = new string[1 + sourceCount * 2 + 1];
            fieldOrder[0] = nameof(Arithmetic.failOnNaN);
            for (int i = 0; i < sourceCount; i++)
            {
                string sourceName = SourceNames[i];
                int offset = 1 + i * 2;
                fieldOrder[offset] = sourceName;
                fieldOrder[offset + 1] = sourceName + LaneSuffix;
            }

            fieldOrder[^1] = VectorResultName;
            return fieldOrder;
        }

        /// <summary>Returns the number of selectable lanes for a native variable type.</summary>
        internal static int GetLaneCount(VariableType type)
        {
            return type switch
            {
                VariableType.Vector2 => 2,
                VariableType.Vector3 => 3,
                VariableType.Vector4 => 4,
                _ => 0,
            };
        }

        /// <summary>Returns the number of selectable lanes for a source field.</summary>
        internal static int GetLaneCount(VariableFieldBase source)
        {
            return source == null ? 0 : GetLaneCount(source.Type);
        }

        /// <summary>Normalizes a serialized lane index to X when it is not valid for the source shape.</summary>
        internal static bool NormalizeLaneIndex(int laneIndex, int componentCount, out int normalizedIndex)
        {
            if (componentCount <= 0)
            {
                normalizedIndex = 0;
                return laneIndex != 0;
            }

            if (laneIndex < 0 || laneIndex >= componentCount)
            {
                normalizedIndex = 0;
                return true;
            }

            normalizedIndex = laneIndex;
            return false;
        }

        private void DrawPropertyRow(string fieldName)
        {
            SerializedProperty fieldProperty = property.FindPropertyRelative(fieldName);
            if (fieldProperty == null)
            {
                return;
            }

            FieldInfo field = NodeDrawerFieldMetadata.GetField(fieldProperty);
            bool shouldDraw;
            try
            {
                shouldDraw = NodeDrawerFieldMetadata.ShouldDraw(node, field);
            }
            catch (Exception)
            {
                EditorGUILayout.LabelField(fieldProperty.displayName, "DisplayIf attribute breaks, ask for help now");
                return;
            }

            if (!shouldDraw)
            {
                return;
            }

            GUIContent label = new(fieldProperty.displayName);

            // VariableField has its own fixed-rect drawer. Calling PropertyField here would
            // let Unity expand the serializable implementation fields in addition to that
            // drawer, which causes the next lane row to be painted over the expansion.
            if (fieldProperty.GetAIValue() is VariableFieldBase variable)
            {
                float variableHeight = VariableFieldDrawers.GetVariableHeight(variable, tree);
                Rect variableRect = EditorGUILayout.GetControlRect(true, variableHeight);
                using (new EditorGUI.DisabledScope(NodeDrawerFieldMetadata.IsReadOnly(field)))
                {
                    VariableFieldDrawers.DrawVariable(variableRect, label, fieldProperty);
                }

                return;
            }

            float height = EditorGUI.GetPropertyHeight(fieldProperty, label, true);
            Rect rect = EditorGUILayout.GetControlRect(true, height);
            using (new EditorGUI.DisabledScope(NodeDrawerFieldMetadata.IsReadOnly(field)))
            {
                EditorGUI.PropertyField(rect, fieldProperty, label, false);
            }
        }

        private void DrawLaneForSource(string sourceName, string laneName)
        {
            SerializedProperty sourceProperty = property.FindPropertyRelative(sourceName);
            SerializedProperty laneProperty = property.FindPropertyRelative(laneName);
            if (laneProperty == null)
            {
                return;
            }

            int componentCount = GetLaneCount(sourceProperty?.GetAIValue() as VariableFieldBase);
            if (componentCount <= 1)
            {
                return;
            }

            GUIContent label = new(laneProperty.displayName);
            Rect rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            GUIContent[] labels = new GUIContent[componentCount];
            Array.Copy(LaneLabels, labels, componentCount);
            FieldInfo field = NodeDrawerFieldMetadata.GetField(laneProperty);
            using (new EditorGUI.DisabledScope(NodeDrawerFieldMetadata.IsReadOnly(field)))
            {
                EditorGUI.BeginProperty(rect, label, laneProperty);
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUI.Popup(rect, label, laneProperty.enumValueIndex, labels);
                bool changed = EditorGUI.EndChangeCheck();
                if (changed)
                {
                    laneProperty.enumValueIndex = selected;
                }

                EditorGUI.EndProperty();
            }
        }

        private static bool TryGetLaneSource(string propertyName, out string sourceName)
        {
            if (string.IsNullOrEmpty(propertyName)
                || !propertyName.EndsWith(LaneSuffix, StringComparison.Ordinal)
                || propertyName.Length == LaneSuffix.Length)
            {
                sourceName = null;
                return false;
            }

            sourceName = propertyName[..^LaneSuffix.Length];
            return true;
        }

    }

    /// <summary>Draws CreateVector2 with source-shape-aware lane selectors.</summary>
    [CustomNodeDrawer(typeof(CreateVector2))]
    internal sealed class CreateVector2Drawer : VectorConstructorDrawerBase
    {
        /// <inheritdoc />
        protected override int SourceCount => 2;
    }

    /// <summary>Draws CreateVector3 with source-shape-aware lane selectors.</summary>
    [CustomNodeDrawer(typeof(CreateVector3))]
    internal sealed class CreateVector3Drawer : VectorConstructorDrawerBase
    {
        /// <inheritdoc />
        protected override int SourceCount => 3;
    }

    /// <summary>Draws CreateVector4 with source-shape-aware lane selectors.</summary>
    [CustomNodeDrawer(typeof(CreateVector4))]
    internal sealed class CreateVector4Drawer : VectorConstructorDrawerBase
    {
        /// <inheritdoc />
        protected override int SourceCount => 4;
    }
}
