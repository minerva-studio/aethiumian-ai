using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Aethiumian.AI.Editor
{
    internal static class VariableDataDefaultValueMigration
    {
        [MenuItem("Window/Aethiumian AI/Migrations/Migrate VariableData Defaults")]
        public static void MigrateAllAssets()
        {
            int assetCount = 0;
            int variableCount = 0;
            int errorCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:BehaviourTreeData"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BehaviourTreeData asset = AssetDatabase.LoadAssetAtPath<BehaviourTreeData>(path);
                if (!asset)
                {
                    continue;
                }

                if (MigrateVariables(asset.variables, path, ref variableCount, ref errorCount))
                {
                    EditorUtility.SetDirty(asset);
                    assetCount++;
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AISetting"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AISetting asset = AssetDatabase.LoadAssetAtPath<AISetting>(path);
                if (!asset)
                {
                    continue;
                }

                if (MigrateVariables(asset.globalVariables, path, ref variableCount, ref errorCount))
                {
                    EditorUtility.SetDirty(asset);
                    assetCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Migrated VariableData defaults: {variableCount} variables in {assetCount} assets. Errors: {errorCount}.");
        }

        private static bool MigrateVariables(
            IReadOnlyList<VariableData> variables,
            string assetPath,
            ref int variableCount,
            ref int errorCount)
        {
            if (variables == null)
            {
                return false;
            }

            bool changed = false;
            foreach (VariableData variable in variables)
            {
                if (variable == null)
                {
                    continue;
                }
                if (variable.Type is VariableType.Invalid or VariableType.Node)
                {
                    continue;
                }

                try
                {
                    object defaultValue = variable.GetDefaultValue();
                    variable.SetDefaultValue(defaultValue);
                    variableCount++;
                    changed = true;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    Debug.LogError($"Failed to migrate default value for variable '{variable.name}' in {assetPath}: {ex.Message}");
                }
            }

            return changed;
        }
    }
}
