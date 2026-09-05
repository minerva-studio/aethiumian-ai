using System;
using System.Linq;
using Aethiumian.AI.Editor.Mutations;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Variables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Variables
{
    /// <summary>Loads historical serialized type names through Unity before invoking the explicit repair.</summary>
    public sealed class TimerNameMigrationTests
    {
        /// <summary>Verifies Historical Service Round Trip Preserves Identity And Fields.</summary>
        [TestCase("BranchCountdown")]
        public void HistoricalServiceRoundTripPreservesIdentityAndFields(string oldName)
        {
            const string testFolder = "Assets/__AethiumianAITestAssets";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{testFolder}/TimerMigration.asset");
            bool folderCreated = false;
            BehaviourTreeData tree = ScriptableObject.CreateInstance<BehaviourTreeData>();
            Sequence head = new() { uuid = UUID.NewUUID(), name = "Host" };
            Countdown service = new() { uuid = UUID.NewUUID(), name = "Authored Countdown" };
            VariableData variable = new("Remaining", VariableType.Float);
            variable.SetDefaultValue(9f);
            service.updatingVariable.SetReference(variable);
            head.AddService(service);
            tree.headNodeUUID = head.uuid;
            tree.nodes.Add(head);
            tree.nodes.Add(service);
            tree.variables.Add(variable);
            try
            {
                if (!AssetDatabase.IsValidFolder(testFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "__AethiumianAITestAssets");
                    folderCreated = true;
                }

                path = AssetDatabase.GenerateUniqueAssetPath($"{testFolder}/TimerMigration.asset");
                AssetDatabase.CreateAsset(tree, path);
                string json = EditorJsonUtility.ToJson(tree);
                string legacy = json.Replace("\"class\":\"Countdown\"", "\"class\":\"" + oldName + "\"");
                Assert.That(legacy, Is.Not.EqualTo(json), "The Unity JSON fixture must contain managed-reference type metadata.");
                EditorJsonUtility.FromJsonOverwrite(legacy, tree);
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssetIfDirty(tree);
                BehaviourTreeEditResult result = TimerNameMigration.Migrate(tree);
                Assert.That(result.Success, Is.True, result.Error);
                Assert.That(result.Saved, Is.True, result.Error);
                Assert.That(UnityEditor.SerializationUtility.GetManagedReferencesWithMissingTypes(tree), Is.Empty);
                Countdown restored = tree.nodes.OfType<Countdown>().Single();
                Assert.That(restored.uuid, Is.EqualTo(service.uuid));
                Assert.That(restored.parent.UUID, Is.EqualTo(head.uuid));
                Assert.That(restored.name, Is.EqualTo("Authored Countdown"));
                Assert.That(restored.updatingVariable.UUID, Is.EqualTo(variable.UUID));
                Assert.That(tree.variables.Single().GetDefaultValue(), Is.EqualTo(9f));
                Assert.That(tree.headNodeUUID, Is.EqualTo(head.uuid));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (folderCreated && AssetDatabase.IsValidFolder(testFolder))
                    AssetDatabase.DeleteAsset(testFolder);
                AssetDatabase.Refresh();
                if (tree) UnityEngine.Object.DestroyImmediate(tree);
            }
        }
    }
}
