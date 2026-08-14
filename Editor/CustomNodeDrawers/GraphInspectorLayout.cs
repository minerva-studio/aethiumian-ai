using System;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    /// <summary>Provides narrow-layout calculations shared by Graph Inspector drawers.</summary>
    internal static class GraphInspectorLayout
    {
        internal readonly struct FunctionSelectionRects
        {
            internal Rect ValueRect { get; }
            internal Rect OverflowRect { get; }

            internal FunctionSelectionRects(Rect valueRect, Rect overflowRect)
            {
                ValueRect = valueRect;
                OverflowRect = overflowRect;
            }
        }

        internal readonly struct TypeReferenceRects
        {
            internal Rect ValueRect { get; }
            internal Rect PickRect { get; }
            internal Rect OverflowRect { get; }

            internal TypeReferenceRects(Rect valueRect, Rect pickRect, Rect overflowRect)
            {
                ValueRect = valueRect;
                PickRect = pickRect;
                OverflowRect = overflowRect;
            }
        }

        internal const float OverflowWidth = 22f;
        internal const float SubtreeWideLayoutBreakpoint = 360f;

        /// <summary>Returns whether a translation row can use the wide two-column layout.</summary>
        internal static bool UseWideSubtreeTranslationLayout(float width)
        {
            return width >= SubtreeWideLayoutBreakpoint;
        }

        /// <summary>Calculates a function value row with only its value and overflow regions.</summary>
        internal static FunctionSelectionRects CalculateFunctionSelectionRects(Rect position)
        {
            float width = Mathf.Max(0f, position.width);
            float overflowWidth = Mathf.Min(OverflowWidth, width);
            Rect valueRect = new(position.x, position.y, Mathf.Max(0f, width - overflowWidth), position.height);
            Rect overflowRect = new(valueRect.xMax, position.y, overflowWidth, position.height);
            return new FunctionSelectionRects(valueRect, overflowRect);
        }

        /// <summary>Calculates a type-reference row with a flexible value and compact actions.</summary>
        internal static TypeReferenceRects CalculateTypeReferenceRects(Rect position, float pickWidth = 52f)
        {
            float width = Mathf.Max(0f, position.width);
            float overflowWidth = Mathf.Min(OverflowWidth, width);
            float remainingWidth = Mathf.Max(0f, width - overflowWidth);
            float actualPickWidth = Mathf.Min(Mathf.Max(0f, pickWidth), remainingWidth);
            float valueWidth = Mathf.Max(0f, remainingWidth - actualPickWidth);
            Rect valueRect = new(position.x, position.y, valueWidth, position.height);
            Rect pickRect = new(valueRect.xMax, position.y, actualPickWidth, position.height);
            Rect overflowRect = new(pickRect.xMax, position.y, overflowWidth, position.height);
            return new TypeReferenceRects(valueRect, pickRect, overflowRect);
        }

        /// <summary>Draws a function value row and routes selection and clear through callbacks.</summary>
        internal static void DrawFunctionSelectionRow(
            string path,
            bool canClear,
            Action<Rect> onSelect,
            Action onClear)
        {
            Rect row = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            FunctionSelectionRects layout = CalculateFunctionSelectionRects(EditorGUI.IndentedRect(row));
            GUIContent value = new(string.IsNullOrEmpty(path) ? "Select..." : path);
            value.tooltip = value.text;
            if (GUI.Button(layout.ValueRect, value, EditorStyles.popup))
            {
                onSelect?.Invoke(layout.ValueRect);
            }

            using (new EditorGUI.DisabledScope(!canClear))
            {
                if (GUI.Button(layout.OverflowRect, "⋮", EditorStyles.miniButton))
                {
                    GenericMenu menu = new();
                    if (canClear)
                    {
                        menu.AddItem(new GUIContent("Clear"), false, () => onClear?.Invoke());
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Clear"));
                    }
                    menu.ShowAsContext();
                }
            }
        }
    }
}
