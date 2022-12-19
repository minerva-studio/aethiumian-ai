using Minerva.Module;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(ScriptCall))]
    public class ScriptCallDrawer : ScriptMethodDrawerBase
    {
        public override void Draw()
        {
            var call = (ScriptCall)node;

            if (Tree.targetScript == null || !Tree.targetScript.GetClass().IsSubclassOf(typeof(Component)))
            {
                EditorGUILayout.LabelField("No target script assigned, please assign a target script");
                return;
            }

            GUILayout.Space(EditorGUIUtility.singleLineHeight);
            var methods = GetMethods();
            call.methodName = SelectMethod(call.methodName, methods);
            var method = methods.FirstOrDefault(m => m.Name == call.methodName);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(call, method);
            DrawResultField(call.result, method);
        }
    }
}