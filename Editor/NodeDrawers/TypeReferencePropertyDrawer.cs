using Amlos.AI.References;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Property drawer for <see cref="TypeReference"/> to ensure serialized undo/redo support.
    /// </summary>
    [CustomPropertyDrawer(typeof(TypeReference), true)]
    public sealed class TypeReferencePropertyDrawer : PropertyDrawer
    {
        private TypeReferenceDrawer typeReferenceDrawer;

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var value = property.boxedValue as TypeReference;
            typeReferenceDrawer ??= new TypeReferenceDrawer(value, label);
            typeReferenceDrawer.Reset(value, label);
            return typeReferenceDrawer.GetHeight();
        }

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var value = property.boxedValue as TypeReference;
            typeReferenceDrawer ??= new TypeReferenceDrawer(value, label);
            typeReferenceDrawer.Reset(value, label);

            EditorGUI.BeginChangeCheck();
            typeReferenceDrawer.Draw(position);
            if (EditorGUI.EndChangeCheck())
            {
                property.boxedValue = typeReferenceDrawer.TypeReference;
                property.serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

    }
}
