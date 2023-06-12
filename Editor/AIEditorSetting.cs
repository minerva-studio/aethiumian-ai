using Amlos.AI.Nodes;
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
        public bool safeMode;
        public bool debugMode;
        public bool useRawDrawer;
        public bool enableGraph;
        public int variableTableEntryWidth = 150;

        public List<MonoScript> commonNodes;



        /// <summary>
        /// Initialize the common nodes 
        /// </summary>
        public void InitializeCommonNodes()
        {
            commonNodes ??= new List<MonoScript>();
            commonNodes.Clear();

            commonNodes.Add(NodeFactory.Scripts[typeof(Sequence)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(Probability)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(Condition)]);

            commonNodes.Add(NodeFactory.Scripts[typeof(Idle)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(Stop)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(Wait)]);

            commonNodes.Add(NodeFactory.Scripts[typeof(Nodes.Animator)]);

            commonNodes.Add(NodeFactory.Scripts[typeof(ComponentAction)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(GetComponentValue)]);
            commonNodes.Add(NodeFactory.Scripts[typeof(SetComponentValue)]);
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
            EditorGUILayout.PropertyField(property);
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

            if (GUILayout.Button("Reset common nodes"))
            {
                InitializeCommonNodes();
            }
        }





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