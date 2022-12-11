using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    public class AIEditorSetting : ScriptableObject
    {
        public const string SETTING_PATH = "Assets/Editor/User/AIEditor.asset";

        public float overviewWindowSize = 200;
        public int overviewHierachyIndentLevel = 5;
        public bool safeMode;
        public bool useRawDrawer;
        public bool enableGraph;
        public int variableTableEntryWidth = 150;

        internal static AIEditorSetting GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AIEditorSetting>(SETTING_PATH);
            if (settings == null)
            {
                Debug.Log("Recreate");
                settings = CreateInstance<AIEditorSetting>();
                AssetDatabase.CreateAsset(settings, SETTING_PATH);
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static AIEditorSetting ResetSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AIEditorSetting>(SETTING_PATH);
            if (settings != null)
            {
                AssetDatabase.DeleteAsset(SETTING_PATH);
            }
            Debug.Log("Recreate");
            settings = CreateInstance<AIEditorSetting>();
            AssetDatabase.CreateAsset(settings, SETTING_PATH);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}