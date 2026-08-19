using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using UnityEditor;

namespace Aethiumian.AI.Editor
{
    [CustomNodeDrawer(typeof(Aggregate))]
    public sealed class AggregateDrawer : NodeDrawerBase
    {
        private NodeReferenceTreeView list;

        /// <inheritdoc />
        public override void Draw()
        {
            if (node is not Aggregate aggregate) return;

            EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(aggregate.resultMode)));
            SerializedProperty listProperty = property.FindPropertyRelative(nameof(aggregate.events));
            list ??= DrawNodeList<NodeReference>(nameof(Aggregate), listProperty);
            list.Draw();

            if (listProperty.arraySize == 0)
            {
                string result = aggregate.resultMode is Aggregate.ResultMode.All or Aggregate.ResultMode.True
                    ? "Success"
                    : "Failed";
                EditorGUILayout.HelpBox($"Empty Aggregate returns {result}.", MessageType.Info);
            }
        }
    }
}
