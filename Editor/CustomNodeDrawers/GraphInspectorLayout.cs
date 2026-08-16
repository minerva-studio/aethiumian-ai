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

        internal readonly struct NodeReferenceRects
        {
            internal Rect IndexRect { get; }
            internal Rect NameRect { get; }
            internal Rect OpenRect { get; }
            internal Rect DeleteRect { get; }
            internal Rect OverflowRect { get; }

            internal NodeReferenceRects(Rect indexRect, Rect nameRect, Rect openRect, Rect deleteRect, Rect overflowRect)
            {
                IndexRect = indexRect;
                NameRect = nameRect;
                OpenRect = openRect;
                DeleteRect = deleteRect;
                OverflowRect = overflowRect;
            }
        }

        internal const float OverflowWidth = 22f;
        internal const float SubtreeWideLayoutBreakpoint = 360f;
        internal const float NodeReferenceWideLayoutBreakpoint = 360f;
        internal const float NodeReferenceIndexWidth = 28f;

        /// <summary>Calculates non-overlapping action regions for one node-reference row.</summary>
        internal static NodeReferenceRects CalculateNodeReferenceRects(Rect position, bool hasActions, bool wide)
        {
            float width = Mathf.Max(0f, position.width);
            float indexWidth = Mathf.Min(NodeReferenceIndexWidth, width);
            Rect index = new(position.x, position.y, indexWidth, position.height);
            if (!hasActions)
            {
                return new NodeReferenceRects(
                    index,
                    new(index.xMax, position.y, Mathf.Max(0f, position.xMax - index.xMax), position.height),
                    Rect.zero,
                    Rect.zero,
                    Rect.zero);
            }

            float actionWidth = wide ? Mathf.Min(96f, Mathf.Max(0f, width - indexWidth)) : Mathf.Min(OverflowWidth, Mathf.Max(0f, width - indexWidth));
            Rect actions = new(position.xMax - actionWidth, position.y, actionWidth, position.height);
            Rect name = new(index.xMax, position.y, Mathf.Max(0f, actions.x - index.xMax), position.height);
            Rect open = Rect.zero;
            Rect delete = Rect.zero;
            Rect overflow = Rect.zero;
            if (wide)
            {
                float buttonWidth = actionWidth / 2f;
                open = new(actions.x, actions.y, buttonWidth, actions.height);
                delete = new(open.xMax, actions.y, Mathf.Max(0f, actions.xMax - open.xMax), actions.height);
            }
            else
            {
                overflow = actions;
            }
            return new NodeReferenceRects(index, name, open, delete, overflow);
        }

        /// <summary>Returns whether a translation row can use the wide two-column layout.</summary>
        internal static bool UseWideSubtreeTranslationLayout(float width)
        {
            return width >= SubtreeWideLayoutBreakpoint;
        }

        /// <summary>Returns whether a node-reference row has room for direct actions.</summary>
        internal static bool UseWideNodeReferenceLayout(float width)
        {
            return width >= NodeReferenceWideLayoutBreakpoint;
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
