using Amlos.AI.Nodes;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Nodes.Probability;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(Probability))]
    public class ProbabilityDrawer : NodeDrawerBase
    {
        NodeReferenceTreeView list;

        public override void Draw()
        {
            if (node is not Probability probability) return;
            SerializedProperty listProperty = property.FindPropertyRelative(nameof(probability.events));
            //DrawProbabilityWeightList(nameof(Probability), probability, probability.events);
            list ??= DrawNodeList<EventWeight>(new GUIContent(nameof(Probability)), listProperty);
            list.Draw();

            if (probability.events.Length == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(Probability)} \"{node.name}\" has no element.", MessageType.Warning);
                return;
            }

            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.Label("Ratio: ");
            using (new GUILayout.HorizontalScope())
            {
                var totalWeight = probability.events.Sum(e => e.weight);
                var rect = EditorGUILayout.GetControlRect();
                var areaSizeX = rect.width;
                var newX = rect.x;
                foreach (var eventWeight in probability.events)
                {
                    var item = eventWeight.reference;
                    rect.x = newX;
                    rect.width = eventWeight.weight / (float)totalWeight * areaSizeX;
                    newX += rect.width;
                    var childNode = tree.GetNode(item);
                    if (childNode != null)
                        GUI.Button(rect, $"{childNode.name} ({(eventWeight.weight / (float)totalWeight).ToString("0.0%")})");
                    else
                        GUI.Button(rect, $"{"Unknown"} ({(eventWeight.weight / (float)totalWeight).ToString("0.0%")})");
                }
            }
        }
    }
}
