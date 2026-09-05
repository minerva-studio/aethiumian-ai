using System;
using System.Linq;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Mutations
{
    /// <summary>Explicitly repairs historical Timer service names and persists the Timer variable source name.</summary>
    public static class TimerNameMigration
    {
        /// <summary>Migrates one selected tree through a rollback-capable repair transaction.</summary>
        public static BehaviourTreeEditResult Migrate(BehaviourTreeData tree)
        {
            if (!tree) throw new ArgumentNullException(nameof(tree));
            var missing = UnityEditor.SerializationUtility.GetManagedReferencesWithMissingTypes(tree)
                .Where(entry => entry.assemblyName == typeof(Countdown).Assembly.GetName().Name
                    && entry.namespaceName == "Aethiumian.AI.Nodes"
                    && (entry.className == "Timer" || entry.className == "BranchCountdown"))
                .ToArray();
            return BehaviourTreeUnsafeEditTransaction.Execute(tree, "Migrate AI timer names", serialized =>
            {
                SerializedProperty nodes = serialized.FindProperty("nodes");
                foreach (var entry in missing)
                {
                    for (int i = 0; i < nodes.arraySize; i++)
                    {
                        SerializedProperty node = nodes.GetArrayElementAtIndex(i);
                        if (node.managedReferenceId != entry.referenceId) continue;
                        node.managedReferenceValue = JsonUtility.FromJson<Countdown>(entry.serializedData);
                    }
                }
                serialized.ApplyModifiedProperties();
                serialized.Update();
            });
        }
    }
}
