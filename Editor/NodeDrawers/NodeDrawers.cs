using Minerva.Module;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    /// <summary>
    /// Node Drawer helper class
    /// </summary>
    public static class NodeDrawers
    {
        private static IntervalMode intervalMode;
        private static float time;

        enum IntervalMode
        {
            frame,
            realTime
        }

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
            var script = Resources.FindObjectsOfTypeAll<MonoScript>().FirstOrDefault(n => n.GetClass() == treeNode.GetType());
            EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
            GUI.enabled = currentStatus;

            if (isReadOnly) EditorGUILayout.LabelField("Name", treeNode.name);
            else treeNode.name = EditorGUILayout.TextField("Name", treeNode.name);
            EditorGUILayout.LabelField("UUID", treeNode.uuid);

            GUILayout.EndVertical();
        }

        public static string GetEditorName(TreeNode node)
        {
            return node.name + $" ({node.GetType().Name.ToTitleCase()})";
        }
    }
}

