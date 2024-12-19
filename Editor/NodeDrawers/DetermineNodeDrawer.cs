using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Linq;
using UnityEditor;
namespace Amlos.AI.Editor
{
    public class DetermineNodeDrawer : NodeDrawerBase
    {
        private const string Label = "This determine does nothing, and it will always return true.";

        public new DetermineBase node => base.node as DetermineBase;

        public override void Draw()
        {
            DrawOtherField();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Determine Properties");

            var comparableDetermine = node as IComparableDetermine;
            var compare = false;
            if (comparableDetermine != null)
            {
                compare = EditorGUILayout.Toggle("Compare", comparableDetermine.Compare);
                comparableDetermine.Compare = compare;
            }
            node.storeResult = EditorGUILayout.Toggle("Store Result", node.storeResult);
            if (compare)
            {
                DrawCompareMode(comparableDetermine);
                DrawVariable("Expect value:", comparableDetermine.Expect);
            }
            if (node.storeResult)
            {
                DrawVariable("Result store to:", node.Result);
                if (compare)
                {
                    DrawVariable("Compare result store to:", comparableDetermine.CompareResult);
                }
            }

            if (comparableDetermine != null && !compare && !node.storeResult)
            {
                EditorGUILayout.HelpBox(Label, MessageType.Error);
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
                EqualitySign mode = comparableDetermine.Mode == CompareSign.equals ? EqualitySign.equals : EqualitySign.notEquals;
                mode = (EqualitySign)EditorGUILayout.EnumPopup("Mode", mode);
                switch (mode)
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
            var type = base.node.GetType();
            var fields = type.GetFields().Except(type.BaseType.GetFields());
            ComparableDetermine<int> com;
            foreach (var field in fields)
            {
                if (field.IsLiteral && !field.IsInitOnly) continue;
                if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Object))) continue;
                if (field.Name == nameof(NodeDrawerBase.node.name)) continue;
                if (field.Name == nameof(NodeDrawerBase.node.uuid)) continue;
                if (field.Name == nameof(NodeDrawerBase.node.parent)) continue;
                if (field.Name == nameof(NodeDrawerBase.node.services)) continue;
                if (field.Name == nameof(NodeDrawerBase.node.behaviourTree)) continue;
                if (field.Name == nameof(node.storeResult)) continue;
                if (field.Name == nameof(com.expect)) continue;
                if (field.Name == nameof(com.result)) continue;
                if (field.Name == nameof(com.compareResult)) continue;
                if (field.Name == nameof(com.mode)) continue;
                if (field.Name == nameof(com.compare)) continue;

                string labelName = field.Name.ToTitleCase();



                bool draw;
                try
                {
                    draw = ConditionalFieldAttribute.IsTrue(base.node, field);
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now");
                    continue;
                }

                if (draw) DrawField(labelName, field, base.node);
            }
        }
    }
}