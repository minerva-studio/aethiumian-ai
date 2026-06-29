using Aethiumian.AI.Nodes;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Author: Codex
    /// Stores user-local preferences for the Aethiumian AI editor.
    /// </summary>
    public class AIEditorSetting : ScriptableObject
    {
        public const string SETTING_PATH = "UserSettings/AethiumianAI/AIEditor.asset";
        public const string LEGACY_SETTING_PATH = "Assets/Editor/User/AIEditor.asset";

        public bool overviewShowService;
        public bool safeMode;
        public bool debugMode;
        public bool useRawDrawer;
        public bool enableGraph;

        public List<MonoScript> commonNodes;

        private static AIEditorSetting instance;



        /// <summary>
        /// Initialize the common nodes 
        /// </summary>
        public void InitializeCommonNodes()
        {
            commonNodes ??= new List<MonoScript>();
            commonNodes.Clear();

            commonNodes.Add(MonoScriptCache.Get<Sequence>());
            commonNodes.Add(MonoScriptCache.Get<Probability>());
            commonNodes.Add(MonoScriptCache.Get<Condition>());

            commonNodes.Add(MonoScriptCache.Get<Idle>());
            commonNodes.Add(MonoScriptCache.Get<Stop>());
            commonNodes.Add(MonoScriptCache.Get<Wait>());

            commonNodes.Add(MonoScriptCache.Get<Nodes.Animator>());

            commonNodes.Add(MonoScriptCache.Get<FunctionAction>());
            commonNodes.Add(MonoScriptCache.Get<GetComponentValue>());
            commonNodes.Add(MonoScriptCache.Get<SetComponentValue>());
        }

        public Type[] GetCommonNodeTypes()
        {
            if (commonNodes == null)
            {
                InitializeCommonNodes();
            }

            List<Type> types = new();
            for (int i = 0; i < commonNodes.Count; i++)
            {
                Type type = commonNodes[i] != null ? commonNodes[i].GetClass() : null;
                if (NodeMenuCache.IsCreatableNodeType(type))
                {
                    types.Add(type);
                }
            }

            return types.ToArray();
        }

        /// <summary>
        /// Removes invalid entries from the common node list.
        /// </summary>
        /// <returns>No return value.</returns>
        internal void SanitizeCommonNodes()
        {
            if (commonNodes == null)
            {
                InitializeCommonNodes();
            }

            for (int i = 0; i < commonNodes.Count; i++)
            {
                var t = commonNodes[i];
                Type type = t != null ? t.GetClass() : null;
                if (!NodeMenuCache.IsCreatableNodeType(type))
                {
                    commonNodes[i] = null;
                }
            }
        }





        internal static AIEditorSetting GetOrCreateSettings()
        {
            if (instance)
            {
                return instance;
            }

            AIEditorSetting settings = LoadUserSettings();
            if (settings == null)
            {
                settings = LoadLegacySettingsCopy();
                if (settings == null)
                {
                    Debug.Log("Recreate");
                    settings = CreateInstance<AIEditorSetting>();
                    settings.name = nameof(AIEditorSetting);
                }

                SaveSettings(settings);
            }

            instance = settings;
            return instance;
        }

        internal static AIEditorSetting ResetSettings()
        {
            Debug.Log("Recreate");
            instance = CreateInstance<AIEditorSetting>();
            instance.name = nameof(AIEditorSetting);
            SaveSettings(instance);
            return instance;
        }

        /// <summary>
        /// Saves the editor settings to Unity's user-local settings folder.
        /// </summary>
        /// <param name="settings">The settings object to save.</param>
        /// <returns>No return value.</returns>
        internal static void SaveSettings(AIEditorSetting settings)
        {
            if (!settings)
            {
                return;
            }

            CreateFolderIfNotExist(SETTING_PATH);
            InternalEditorUtility.SaveToSerializedFileAndForget(new UnityEngine.Object[] { settings }, SETTING_PATH, true);
        }

        /// <summary>
        /// Gets whether a legacy project asset is available for manual cleanup guidance.
        /// </summary>
        /// <returns>True when the old AssetDatabase-backed settings asset exists.</returns>
        internal static bool HasLegacySettings()
        {
            return AssetDatabase.LoadAssetAtPath<AIEditorSetting>(LEGACY_SETTING_PATH);
        }

        private static AIEditorSetting LoadUserSettings()
        {
            if (!File.Exists(SETTING_PATH))
            {
                return null;
            }

            UnityEngine.Object[] objects = InternalEditorUtility.LoadSerializedFileAndForget(SETTING_PATH);
            AIEditorSetting settings = objects.OfType<AIEditorSetting>().FirstOrDefault();
            if (settings)
            {
                settings.name = nameof(AIEditorSetting);
            }

            return settings;
        }

        private static AIEditorSetting LoadLegacySettingsCopy()
        {
            AIEditorSetting legacySettings = AssetDatabase.LoadAssetAtPath<AIEditorSetting>(LEGACY_SETTING_PATH);
            if (!legacySettings)
            {
                return null;
            }

            AIEditorSetting settings = Instantiate(legacySettings);
            settings.name = nameof(AIEditorSetting);
            return settings;
        }

        private static void CreateFolderIfNotExist(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            Directory.CreateDirectory(directory);
        }
    }
}
