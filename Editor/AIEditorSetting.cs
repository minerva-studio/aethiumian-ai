using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    public class AIEditorSetting : ScriptableObject
    {
        public const string SETTING_PATH = "Assets/Editor/User/AIEditor.asset";

        public float overviewWindowSize = 200;
        public int overviewHierachyIndentLevel = 5;
        public bool overviewShowService;
        public bool safeMode;
        public bool debugMode;
        public bool useRawDrawer;
        public bool useSerializationPropertyDrawer;
        public bool enableGraph;
        public Color HierachyColor = new Color(0f, 0f, 0f, 0.3f);
        public int variableTableEntryWidth = 150;

        public List<MonoScript> commonNodes;




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

            commonNodes.Add(MonoScriptCache.Get<ComponentAction>());
            commonNodes.Add(MonoScriptCache.Get<GetComponentValue>());
            commonNodes.Add(MonoScriptCache.Get<SetComponentValue>());
        }

        public Type[] GetCommonNodeTypes()
        {
            Type[] types = new Type[commonNodes.Count];
            for (int i = 0; i < commonNodes.Count; i++)
            {
                types[i] = commonNodes[i].GetClass();
            }
            return types;
        }

        internal void DrawCommonNodesEditor()
        {
            if (commonNodes == null)
            {
                InitializeCommonNodes();
            }

            EditorUtility.SetDirty(this);
            SerializedObject obj = new(this);
            SerializedProperty property = obj.FindProperty(nameof(commonNodes));
            EditorGUILayout.PropertyField(property, new GUIContent("Common usage", "A list of nodes that will show on the top of the node creation list"));
            if (obj.hasModifiedProperties)
            {
                obj.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
            }
            obj.Update();
            for (int i = 0; i < commonNodes.Count; i++)
            {
                var t = commonNodes[i];
                if (t == null || t.GetClass() == null || !t.GetClass().IsSubclassOf(typeof(TreeNode)) || t.GetClass().IsAbstract)
                    commonNodes[i] = null;
            }
            obj.Dispose();
            //commonNodes.RemoveAll(t => t.GetClass() == null
            //|| !t.GetClass().IsSubclassOf(typeof(TreeNode))
            //|| t.GetClass().IsAbstract
            //);
        }





        internal static AIEditorSetting GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<AIEditorSetting>(SETTING_PATH);
            if (settings == null)
            {
                Debug.Log("Recreate");
                settings = CreateInstance<AIEditorSetting>();
                CreateFolderIfNotExist(SETTING_PATH);
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
            CreateFolderIfNotExist(SETTING_PATH);
            AssetDatabase.CreateAsset(settings, SETTING_PATH);
            AssetDatabase.SaveAssets();
            return settings;
        }

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
    }
}