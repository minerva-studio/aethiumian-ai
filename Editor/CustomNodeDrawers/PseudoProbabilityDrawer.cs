using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module.WeightedRandom;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Amlos.AI.Nodes.PseudoProbability;
namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(PseudoProbability))]
    public class PseudoProbabilityDrawer : NodeDrawerBase
    {
        NodeReferenceTreeView list;

        [Header("Cache")]
        int[] weights;
        int total;
        int lastConsecutive;
        Dictionary<EventWeight, int> weightMap;

        public override void Draw()
        {
            if (node is not PseudoProbability probability) return;
            DrawVariable("Max Consecutive Branch", probability.maxConsecutiveBranch);
            SerializedProperty listProperty = property.FindPropertyRelative(nameof(probability.events));
            //DrawProbabilityWeightList(nameof(Probability), probability, probability.events);
            list ??= DrawNodeList<EventWeight>(new GUIContent(nameof(PseudoProbability)), listProperty);
            list.Draw();

            if (probability.events.Length == 0)
            {
                EditorGUILayout.HelpBox($"{nameof(PseudoProbability)} \"{node.name}\" has no element.", MessageType.Warning);
                return;
            }

            GUILayout.Space(EditorGUI.indentLevel * 16);
            GUILayout.Label("Ratio: ");
            using (new GUILayout.HorizontalScope())
            {
                var totalWeight = probability.events.Sum(e => Mathf.Max(0.1f, GetCurrentValue(e.weight)));
                var rect = EditorGUILayout.GetControlRect();
                var areaSizeX = rect.width;
                var newX = rect.x;
                foreach (var eventWeight in probability.events)
                {
                    var item = eventWeight.reference;
                    rect.x = newX;
                    float weight = Mathf.Max(0.1f, GetCurrentValue(eventWeight.weight));
                    rect.width = weight / totalWeight * areaSizeX;
                    newX += rect.width;
                    var childNode = tree.GetNode(item);
                    if (childNode != null)
                        GUI.Button(rect, $"{childNode.name} ({weight / totalWeight:0.0%})");
                    else
                        GUI.Button(rect, $"{"Unknown"} ({weight / totalWeight:0.0%})");
                }
            }
            int simulationCount = 2000;
            // update cache
            if (weights == null
                || weights.Length != probability.events.Length
                || lastConsecutive != GetCurrentValue(probability.maxConsecutiveBranch)
                //|| !weights.SequenceEqual(probability.events.Select(e => GetCurrentValue(e.weight)))
                )
            {
                RunEstimate(probability, simulationCount);
            }
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Estimate Ratio: ");
                if (GUILayout.Button("Refresh")) RunEstimate(probability, simulationCount);
            }
            using (new GUILayout.HorizontalScope())
            {
                float totalWeight = total;
                var rect = EditorGUILayout.GetControlRect();
                var areaSizeX = rect.width;
                var newX = rect.x;
                foreach (var eventWeight in probability.events)
                {
                    var item = eventWeight.reference;
                    rect.x = newX;
                    if (!weightMap.TryGetValue(eventWeight, out int weightValue))
                    {
                        weightValue = 0;
                    }
                    float weight = Mathf.Max(weightValue, 0.1f);
                    rect.width = weight / totalWeight * areaSizeX;
                    newX += rect.width;
                    var childNode = tree.GetNode(item);
                    if (childNode != null)
                        GUI.Button(rect, $"{childNode.name} ({weight / totalWeight:0.0%})");
                    else
                        GUI.Button(rect, $"{"Unknown"} ({weight / totalWeight:0.0%})");
                }
            }
        }

        private void RunEstimate(PseudoProbability probability, int simulationCount)
        {
            lastConsecutive = GetCurrentValue(probability.maxConsecutiveBranch);
            weights = probability.events.Select(e => GetCurrentValue(e.weight)).ToArray();
            (total, weightMap) = Simulate(simulationCount);
        }

        private (int, Dictionary<EventWeight, int>) Simulate(int simulation = 1000)
        {
            Dictionary<EventWeight, int> dictionary = new Dictionary<EventWeight, int>();

            if (node is not PseudoProbability probability)
            {
                return (0, dictionary);
            }
            int sum = simulation;
            foreach (var eventWeight in probability.events)
            {
                // bias, in case of event can be pick
                int v = GetCurrentValue(eventWeight.weight) <= 0 ? 0 : 1;
                dictionary[eventWeight] = v;
                sum += v;
            }
            int consecutiveCount = 0;
            EventWeight last = null;
            int max = GetCurrentValue(probability.maxConsecutiveBranch);
            for (int i = 0; i < simulation; i++)
            {
                EventWeight eventWeight;
                var biased = new List<Weight<EventWeight>>(probability.events.Select(e => new Weight<EventWeight>(e, GetCurrentValue(e.weight))));
                if (max > 0 && consecutiveCount >= max)
                {
                    biased.RemoveAll(w => w.item == last);
                }
                eventWeight = biased.WeightNode().Item;
                dictionary[eventWeight]++;
                if (last == eventWeight)
                {
                    consecutiveCount++;
                }
                else
                {
                    consecutiveCount = 1;
                    last = eventWeight;
                }
            }
            return (sum, dictionary);
        }


        private int GetCurrentValue(VariableField<int> weight)
        {
            if (weight.IsConstant) return weight.Constant;
            if (weight.HasEditorReference)
            {
                var data = tree.GetVariable(weight.UUID);
                if (data != null && int.TryParse(data.DefaultValue, out var i)) return i;
            }
            return 0;
        }
    }
}
