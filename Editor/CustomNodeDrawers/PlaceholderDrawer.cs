using Amlos.AI;
using UnityEditor;
using UnityEngine;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(PlaceholderNode))]
    public class PlaceholderDrawer : NodeDrawerBase
    {
        string newType = nameof(PlaceholderNode);
        public override void Draw()
        {
            PlaceholderNode node = this.node as PlaceholderNode;

            var textColor = GUI.contentColor;
            GUI.contentColor = Color.red;
            EditorGUILayout.LabelField("This is a broken node!");
            GUI.contentColor = textColor;

            newType = EditorGUILayout.TextField("Convert to type", newType);
            if (GUILayout.Button("Convert to " + newType))
            {
                //var genericNode = tree.treeNodes.Find(n => n.uuid == node.uuid);
                //genericNode.type = newType;
                //editor.Reshadow();
            }

        }
    }
}