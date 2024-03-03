using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using UnityEditor;
using UnityEngine;
namespace Amlos.AI.Editor
{
    /// <summary>
    /// Node Drawer helper class
    /// </summary>
    public static class NodeDrawers
    {
        public static bool showUUID = false;
        private static IntervalMode intervalMode;
        private static float time;

        enum IntervalMode
        {
            frame,
            realTime
        }

        [Obsolete]
        public static void DrawNodeBaseInfo(TreeNode treeNode, bool isReadOnly = false)
        {
            if (treeNode == null)
            {
                //no node
                return;
            }
            GUILayout.BeginVertical();
            var currentStatus = GUI.enabled;
            GUI.enabled = false;
            var script = MonoScriptCache.Get(treeNode.GetType());
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            GUI.enabled = currentStatus;

            if (isReadOnly) EditorGUILayout.LabelField("Name", treeNode.name);
            else treeNode.name = EditorGUILayout.TextField("Name", treeNode.name);
            if (showUUID) EditorGUILayout.LabelField("UUID", treeNode.uuid);

            GUILayout.EndVertical();
        }

        public static void DrawNodeBaseInfo(BehaviourTreeData tree, TreeNode treeNode, bool isReadOnly = false)
        {
            if (treeNode == null)
            {
                //no node
                return;
            }
            GUILayout.BeginVertical();
            var currentStatus = GUI.enabled;
            GUI.enabled = false;
            var script = MonoScriptCache.Get(treeNode.GetType());
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            GUI.enabled = currentStatus;

            tree.SerializedObject.Update();
            var property = tree.GetNodeProperty(treeNode);
            if (isReadOnly) EditorGUILayout.LabelField("Name", treeNode.name);
            else EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(treeNode.name)));
            if (showUUID) EditorGUILayout.LabelField("UUID", treeNode.uuid);

            if (property.serializedObject.hasModifiedProperties)
            {
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
            }
            GUILayout.EndVertical();
        }

        public static string GetEditorName(TreeNode node)
        {
            return node.name + $" ({node.GetType().Name.ToTitleCase()})";
        }
    }
}

