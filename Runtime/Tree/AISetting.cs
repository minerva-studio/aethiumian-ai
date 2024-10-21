using Amlos.AI.Variables;
using Minerva.Module;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI
{
    public class AISetting : ScriptableObject
    {
        public const string EDITOR_SETTING_PATH = "Assets/Resources/" + SETTING_PATH + ".asset";
        public const string SETTING_PATH = "AI/AISettings";



        public List<VariableData> globalVariables = new();



        private static AISetting instance;
        public static AISetting Instance { get => instance = instance ? instance : instance = GetOrCreateSettings(); }

        public static AISetting GetOrCreateSettings()
        {
            var settings = Resources.Load<AISetting>(SETTING_PATH);
            if (settings) return settings;

#if UNITY_EDITOR
            Debug.Log("Recreate");
            settings = CreateInstance<AISetting>();
            CreateFolderIfNotExist(EDITOR_SETTING_PATH);
            UnityEditor.AssetDatabase.CreateAsset(settings, EDITOR_SETTING_PATH);
            UnityEditor.AssetDatabase.SaveAssets();
#else
            return null;
#endif
            return settings;
        }

#if UNITY_EDITOR
        public static void CreateFolderIfNotExist(string path)
        {
            string[] strings = path.Split('/');
            for (int i = 1; i < strings.Length - 1; i++)
            {
                string localFolderPath = string.Join('/', strings[0..(i + 1)]);
                if (!AssetDatabase.IsValidFolder(localFolderPath))
                {
                    string parentFolder = string.Join('/', strings[0..i]);
                    AssetDatabase.CreateFolder(parentFolder, strings[i]);
                }
            }
        }

        public static UnityEditor.SerializedObject GetSerializedSettings()
        {
            return new UnityEditor.SerializedObject(GetOrCreateSettings());
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable data by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetGlobalVariableData(string varName)
        {
            globalVariables ??= new List<VariableData>();
            return globalVariables.FirstOrDefault(v => v.name == varName);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetGlobalVariableData(UUID uuid)
        {
            globalVariables ??= new List<VariableData>();
            return globalVariables.FirstOrDefault(v => v.UUID == uuid);
        }
#endif
    }
}
