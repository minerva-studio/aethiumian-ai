using Amlos.AI.Nodes;
using Minerva.Module;
using Minerva.Module.Editor;
using System;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Node Drawer helper class
    /// </summary>
    public static class NodeDrawerUtility
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

            using var verticalScope = new GUILayout.VerticalScope();

            var script = MonoScriptCache.Get(treeNode.GetType());
            using (GUIEnable.By(false))
                EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);

            var property = tree.GetNodeProperty(treeNode);
            // property is outdated, then return 
            if (property == null)
            {
                EditorGUILayout.LabelField("Outdated");
                treeNode.name = EditorGUILayout.TextField(treeNode.name);
                if (showUUID) EditorGUILayout.LabelField("UUID", treeNode.uuid);
            }
            else
            {
                if (isReadOnly) EditorGUILayout.LabelField("Name", treeNode.name);
                else EditorGUILayout.PropertyField(property.FindPropertyRelative(nameof(treeNode.name)));
                if (showUUID) EditorGUILayout.LabelField("UUID", treeNode.uuid);

                if (property.serializedObject.hasModifiedProperties)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                }
            }
        }

        public static string GetEditorName(TreeNode node)
        {
            return node.name + $" ({node.GetType().Name.ToTitleCase()})";
        }
    }
}

