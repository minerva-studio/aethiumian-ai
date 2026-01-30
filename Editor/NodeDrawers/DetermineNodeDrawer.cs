using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using System.Reflection;
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

        /// <summary>
        /// Draw extra fields that belong to this node only.
        /// </summary>
        private void DrawOtherField()
        {
            ComparableDetermine<int> com;
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();

            iterator.NextVisible(true);
            while (!SerializedProperty.EqualContents(iterator, endProperty))
            {
                var field = iterator.GetMemberInfo() as FieldInfo;
                if (field == null)
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.IsLiteral && !field.IsInitOnly)
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.FieldType.IsSubclassOf(typeof(UnityEngine.Object)))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(NodeDrawerBase.node.name))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(NodeDrawerBase.node.uuid))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(NodeDrawerBase.node.parent))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(NodeDrawerBase.node.services))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(NodeDrawerBase.node.behaviourTree))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(node.storeResult))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(com.expect))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(com.result))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(com.compareResult))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(com.mode))
                {
                    iterator.NextVisible(false);
                    continue;
                }
                if (field.Name == nameof(com.compare))
                {
                    iterator.NextVisible(false);
                    continue;
                }

                string labelName = field.Name.ToTitleCase();

                bool draw;
                try
                {
                    draw = ConditionalFieldAttribute.IsTrue(base.node, field);
                }
                catch (Exception)
                {
                    EditorGUILayout.LabelField(labelName, "DisplayIf attribute breaks, ask for help now");
                    iterator.NextVisible(false);
                    continue;
                }

                if (draw)
                {
                    DrawProperty(iterator);
                }

                iterator.NextVisible(false);
            }
        }
    }
}
