using Minerva.Module;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public class DetermineNodeDrawer : NodeDrawerBase
    {
        private const string Label = "This determine does nothing, and it will always return true.";

        public DetermineBase Node => node as DetermineBase;
        public override void Draw()
        {
            DrawOtherField();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Determine Properties");

            var comparableDetermine = Node as IComparableDetermine;
            var compare = false;
            if (comparableDetermine != null)
            {
                compare = EditorGUILayout.Toggle("Compare", comparableDetermine.Compare);
                comparableDetermine.Compare = compare;
            }
            Node.storeResult = EditorGUILayout.Toggle("Store Result", Node.storeResult);
            if (compare)
            {
                DrawCompareMode(comparableDetermine);
                DrawVariable("Expect value:", comparableDetermine.Expect);
            }
            if (Node.storeResult)
            {
                DrawVariable("Result store to:", Node.Result);
                if (compare)
                {
                    DrawVariable("Compare result store to:", comparableDetermine.CompareResult);
                }
            }

            if (comparableDetermine != null && !compare && !Node.storeResult)
            {
                var oldColor = GUI.contentColor;
                GUI.contentColor = Color.red;
                EditorGUILayout.LabelField(Label);
                GUI.contentColor = oldColor;
            }
        }

        private static void DrawCompareMode(IComparableDetermine comparableDetermine)
        {
            if (comparableDetermine.CanPerformComparison)
            {
                comparableDetermine.Mode = (CompareSign)EditorGUILayout.EnumPopup("Mode", comparableDetermine.Mode);
            }
            else
            {
                var eSign = (EqualitySign)EditorGUILayout.EnumPopup("Mode", comparableDetermine.Mode.ToEqualityCheck());
                switch (eSign)
                {
                    case EqualitySign.notEquals:
                        comparableDetermine.Mode = CompareSign.notEquals;
                        break;
                    case EqualitySign.equals:
                        comparableDetermine.Mode = CompareSign.equals;
                        break;
                }
            }
        }

        private void DrawOtherField()
        {
            var type = node.GetType();
            var fields = type.GetFields().Except(type.BaseType.GetFields());
            ComparableDetermine<int> com;
            foreach (var field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly) continue;
                if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) continue;
                if (field.Name == nameof(node.name)) continue;
                if (field.Name == nameof(node.uuid)) continue;
                if (field.Name == nameof(node.parent)) continue;
                if (field.Name == nameof(node.services)) continue;
                if (field.Name == nameof(node.behaviourTree)) continue;
                if (field.Name == nameof(Node.storeResult)) continue;
                if (field.Name == nameof(com.expect)) continue;
                if (field.Name == nameof(com.result)) continue;
                if (field.Name == nameof(com.compareResult)) continue;
                if (field.Name == nameof(com.mode)) continue;
                if (field.Name == nameof(com.compare)) continue;

                string labelName = field.Name.ToTitleCase();



                bool draw;
                try
                {
                    draw = ConditionalFieldAttribute.IsTrue(node, field);
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now");
                    continue;
                }

                if (draw) DrawField(labelName, field, node);
            }
        }
    }
}