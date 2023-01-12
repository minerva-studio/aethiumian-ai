using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomEditor(typeof(BehaviourTreeData))]
    public class BehaviourTreeDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var height = GUILayout.Height(27);
            GUILayout.FlexibleSpace();
            BehaviourTreeData data = (BehaviourTreeData)target;

            GUILayout.Label("Directly edit Behaviour Tree Data is debug-only. Please use AI Editor to edit");
            GUILayout.Space(10);
            if (GUILayout.Button("Open AI Editor"))
            {
                var window = AIEditorWindow.ShowWindow();
                window.Load(data);
            }

            base.OnInspectorGUI();
        }

    }
}