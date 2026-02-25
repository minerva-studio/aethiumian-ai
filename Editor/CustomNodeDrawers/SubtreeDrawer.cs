using Amlos.AI.Nodes;
using Amlos.AI.Variables;
using Minerva.Module;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Draws the editor UI for subtree nodes with variable translation support.
    /// </summary>
    [CustomNodeDrawer(typeof(Subtree))]
    public class SubtreeNodeDrawer : NodeDrawerBase
    {
        private const float VariableNameWidth = 220f;
        private static readonly GUIContent VariableTranslationHeader = new("Variable Translation");

        /// <summary>
        /// Draws the subtree editor UI, including variable translation mapping.
        /// </summary>
        /// <remarks>
        /// This method ensures translation entries are synchronized before rendering.
        /// </remarks>
        public override void Draw()
        {
            if (node is not Subtree subtree)
            {
                return;
            }

            if (property == null)
            {
                EditorGUILayout.HelpBox("Serialized property is missing for subtree drawer.", MessageType.Error);
                return;
            }

            SerializedProperty behaviourTreeProperty = property.FindPropertyRelative(nameof(Subtree.behaviourTreeData));
            SerializedProperty variableTableProperty = property.FindPropertyRelative(nameof(Subtree.variableTable));
            SerializedProperty entriesProperty = variableTableProperty?.FindPropertyRelative(nameof(VariableTableTranslationBuilder.entries));

            DrawProperty(behaviourTreeProperty);

            if (subtree.behaviourTreeData == null)
            {
                EditorGUILayout.HelpBox("Subtree data is missing. Assign a Behaviour Tree Data asset to edit translations.", MessageType.Info);
                return;
            }

            if (entriesProperty == null)
            {
                EditorGUILayout.HelpBox("Variable table translation data is missing.", MessageType.Error);
                return;
            }

            List<VariableData> subtreeVariables = GetSubtreeVariables(subtree.behaviourTreeData);
            if (subtreeVariables.Count == 0)
            {
                EditorGUILayout.HelpBox("No variables are defined in the subtree data.", MessageType.Info);
                return;
            }

            if (tree == null)
            {
                EditorGUILayout.HelpBox("Parent behaviour tree data is missing.", MessageType.Error);
                return;
            }

            PruneTranslationEntries(entriesProperty, subtreeVariables);

            DrawVariableTranslationTable(entriesProperty, subtreeVariables, tree);
        }

        /// <summary>
        /// Draws the variable translation mapping table.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="subtreeVariables">Variables declared in the subtree data.</param>
        /// <param name="parentTree">The parent behaviour tree that supplies variable options.</param>
        /// <returns>No return value.</returns>
        private static void DrawVariableTranslationTable(
            SerializedProperty entriesProperty,
            IReadOnlyList<VariableData> subtreeVariables,
            BehaviourTreeData parentTree)
        {
            if (entriesProperty == null || subtreeVariables == null || parentTree == null)
            {
                return;
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(VariableTranslationHeader, EditorStyles.boldLabel);

            using (new GUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Subtree Variable", EditorStyles.miniBoldLabel, GUILayout.Width(VariableNameWidth));
                EditorGUILayout.LabelField("Parent Variable", EditorStyles.miniBoldLabel);
            }

            List<VariableData> parentVariables = GetParentVariables(parentTree);
            foreach (VariableData variable in subtreeVariables)
            {
                DrawVariableTranslationRow(entriesProperty, variable, parentTree, parentVariables);
            }
        }

        /// <summary>
        /// Draws a single translation row for a subtree variable.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="subtreeVariable">The subtree variable to map.</param>
        /// <param name="parentTree">The parent behaviour tree used for display names.</param>
        /// <param name="parentVariables">The available parent variables.</param>
        /// <returns>No return value.</returns>
        private static void DrawVariableTranslationRow(
            SerializedProperty entriesProperty,
            VariableData subtreeVariable,
            BehaviourTreeData parentTree,
            IReadOnlyList<VariableData> parentVariables)
        {
            if (entriesProperty == null || subtreeVariable == null || parentTree == null || parentVariables == null)
            {
                return;
            }

            using (new GUILayout.HorizontalScope())
            {
                string subtreeLabel = $"{subtreeVariable.name} ({subtreeVariable.Type})";
                EditorGUILayout.LabelField(subtreeLabel, GUILayout.Width(VariableNameWidth));

                if (!TryGetEntry(entriesProperty, subtreeVariable.UUID, out SerializedProperty entryProperty, out int entryIndex))
                {
                    EditorGUILayout.LabelField("Not mapped");
                    if (GUILayout.Button("Add", GUILayout.Width(60f)))
                    {
                        AddEntry(entriesProperty, subtreeVariable.UUID);
                    }
                    return;
                }

                var entry = entryProperty.boxedValue is VariableTranslationTable.Entry e ? e : default;
                UUID currentTarget = entry.to;
                (GUIContent[] labels, List<UUID> uuids, int index) = BuildParentOptions(parentTree, parentVariables, currentTarget);
                int newIndex = EditorGUILayout.Popup(index, labels);
                if (newIndex != index && newIndex >= 0 && newIndex < uuids.Count)
                {
                    entry.to = uuids[newIndex];
                    entryProperty.boxedValue = entry;
                    entriesProperty.serializedObject.ApplyModifiedProperties();
                    entriesProperty.serializedObject.Update();
                }

                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    RemoveEntry(entriesProperty, entryIndex);
                }
            }
        }

        /// <summary>
        /// Builds the parent variable options for a translation row.
        /// </summary>
        /// <param name="parentTree">Parent tree used to render descriptive names.</param>
        /// <param name="parentVariables">Variables available in the parent tree.</param>
        /// <param name="currentTarget">The currently selected parent UUID.</param>
        /// <returns>A tuple containing popup labels, UUID list, and the selected index.</returns>
        private static (GUIContent[] labels, List<UUID> uuids, int index) BuildParentOptions(
            BehaviourTreeData parentTree,
            IReadOnlyList<VariableData> parentVariables,
            UUID currentTarget)
        {
            if (parentTree == null || parentVariables == null)
            {
                return (new[] { new GUIContent(VariableData.NONE_VARIABLE_NAME) }, new List<UUID> { UUID.Empty }, 0);
            }

            var uuids = new List<UUID> { UUID.Empty };
            var labels = new List<GUIContent> { new GUIContent(VariableData.NONE_VARIABLE_NAME) };

            foreach (VariableData variable in parentVariables)
            {
                uuids.Add(variable.UUID);
                labels.Add(new GUIContent(parentTree.GetVariableDescName(variable)));
            }

            int index = uuids.IndexOf(currentTarget);
            if (index < 0 && currentTarget != UUID.Empty)
            {
                uuids.Add(currentTarget);
                labels.Add(new GUIContent($"Missing ({currentTarget})"));
                index = uuids.Count - 1;
            }

            return (labels.ToArray(), uuids, index);
        }

        /// <summary>
        /// Removes invalid or duplicate translation entries.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="subtreeVariables">Variables declared in the subtree data.</param>
        /// <returns>No return value.</returns>
        private static void PruneTranslationEntries(SerializedProperty entriesProperty, IReadOnlyList<VariableData> subtreeVariables)
        {
            if (entriesProperty == null || subtreeVariables == null)
            {
                return;
            }

            var subtreeUuids = new HashSet<UUID>(subtreeVariables.Select(v => v.UUID));
            var seen = new HashSet<UUID>();

            for (int i = entriesProperty.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                var entry = (VariableTranslationTable.Entry)entryProperty.boxedValue;
                //SerializedProperty fromProperty = entryProperty.FindPropertyRelative(nameof(VariableTranslationTable.Entry.from));
                UUID fromUuid = entry.from;

                if (fromUuid == UUID.Empty || !subtreeUuids.Contains(fromUuid) || !seen.Add(fromUuid))
                {
                    entriesProperty.DeleteArrayElementAtIndex(i);
                }
            }

            entriesProperty.serializedObject.ApplyModifiedProperties();
            entriesProperty.serializedObject.Update();
        }

        /// <summary>
        /// Finds the translation entry for a subtree variable UUID.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="fromUuid">The subtree variable UUID.</param>
        /// <param name="entryProperty">The matching serialized entry property.</param>
        /// <param name="entryIndex">The index of the matching entry.</param>
        /// <returns>True when an entry is found.</returns>
        private static bool TryGetEntry(SerializedProperty entriesProperty, UUID fromUuid, out SerializedProperty entryProperty, out int entryIndex)
        {
            if (entriesProperty == null)
            {
                entryProperty = null;
                entryIndex = -1;
                return false;
            }

            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                SerializedProperty candidateProperty = entriesProperty.GetArrayElementAtIndex(i);
                var candidate = (VariableTranslationTable.Entry)candidateProperty.boxedValue;
                UUID entryUuid = candidate.from;
                if (entryUuid == fromUuid)
                {
                    entryProperty = candidateProperty;
                    entryIndex = i;
                    return true;
                }
            }

            entryProperty = null;
            entryIndex = -1;
            return false;
        }

        /// <summary>
        /// Adds a new translation entry for the specified subtree variable UUID.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="fromUuid">The subtree variable UUID to map.</param>
        /// <returns>No return value.</returns>
        /// <remarks>
        /// Writes the full <see cref="VariableTranslationTable.Entry"/> struct to ensure serialization updates.
        /// </remarks>
        private static void AddEntry(SerializedProperty entriesProperty, UUID fromUuid)
        {
            if (entriesProperty == null)
            {
                Debug.LogError("Cannot add translation entry: entriesProperty is null.");
                return;
            }

            SerializedObject serializedObject = entriesProperty.serializedObject;
            Undo.RecordObjects(serializedObject.targetObjects, "Add Variable Translation Entry");
            serializedObject.Update();

            int index = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entryProperty = entriesProperty.GetArrayElementAtIndex(index);
            entryProperty.boxedValue = new VariableTranslationTable.Entry
            {
                from = fromUuid,
                to = UUID.Empty
            };

            serializedObject.ApplyModifiedProperties();

            foreach (Object target in serializedObject.targetObjects)
            {
                EditorUtility.SetDirty(target);
            }

            serializedObject.Update();
        }

        /// <summary>
        /// Removes a translation entry at the specified index.
        /// </summary>
        /// <param name="entriesProperty">Serialized array property that stores translation entries.</param>
        /// <param name="entryIndex">The index of the entry to remove.</param>
        /// <returns>No return value.</returns>
        private static void RemoveEntry(SerializedProperty entriesProperty, int entryIndex)
        {
            if (entriesProperty == null || entryIndex < 0 || entryIndex >= entriesProperty.arraySize)
            {
                return;
            }

            SerializedObject serializedObject = entriesProperty.serializedObject;
            Undo.RecordObjects(serializedObject.targetObjects, "Remove Variable Translation Entry");
            serializedObject.Update();

            entriesProperty.DeleteArrayElementAtIndex(entryIndex);
            serializedObject.ApplyModifiedProperties();

            foreach (Object target in serializedObject.targetObjects)
            {
                EditorUtility.SetDirty(target);
            }

            serializedObject.Update();
        }

        /// <summary>
        /// Gets the list of variables defined in the subtree data.
        /// </summary>
        /// <param name="subtreeData">The subtree data to read variables from.</param>
        /// <returns>A list of subtree variables in display order.</returns>
        private static List<VariableData> GetSubtreeVariables(BehaviourTreeData subtreeData)
        {
            if (subtreeData == null)
            {
                return new List<VariableData>();
            }

            return subtreeData.EditorVariables
                .Where(variable => variable != null)
                .OrderBy(variable => variable.name)
                .ToList();
        }

        /// <summary>
        /// Gets the list of variables available in the parent tree for translation.
        /// </summary>
        /// <param name="parentTree">The parent behaviour tree to read variables from.</param>
        /// <returns>A list of parent variables available for mapping.</returns>
        private static List<VariableData> GetParentVariables(BehaviourTreeData parentTree)
        {
            if (parentTree == null)
            {
                return new List<VariableData>();
            }

            var variables = parentTree.EditorVariables
                .Union(AISetting.Instance.globalVariables)
                .Where(variable => variable != null)
                .GroupBy(variable => variable.UUID)
                .Select(group => group.First())
                .ToList();

            variables.Add(VariableData.GameObjectVariable);
            variables.Add(VariableData.TransformVariable);
            variables.Add(VariableData.TargetScriptVariable);

            return variables;
        }
    }
}
