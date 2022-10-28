using Amlos.AI;
using Minerva.Module;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;

namespace Amlos.Editor
{
    [CustomNodeDrawer(typeof(ScriptStartCoroutine))]
    public class ScriptStartCoroutineDrawer : NodeDrawerBase
    {
        int selected;
        public override void Draw()
        {
            if (this.node is not ScriptStartCoroutine ssc) return;
            if (tree.targetScript)
            {
                string[] options = GetOptions();
                if (options.Length == 0)
                {
                    EditorGUILayout.LabelField("Method Name", "No valid method found");
                }
                else
                {
                    selected = Array.IndexOf(options, ssc.methodName);
                    if (selected < 0)
                    {
                        selected = 0;
                    }

                    selected = EditorGUILayout.Popup("Method Name", selected, options);
                    ssc.methodName = options[selected];
                }
            }
            else
            {
                ssc.methodName = EditorGUILayout.TextField("Method Name", ssc.methodName);
            }

            ssc.afterExecuteAction = (ScriptStartCoroutine.AfterExecuteAction)EditorGUILayout.EnumPopup("After Execution Action", ssc.afterExecuteAction);
        }


        protected string[] GetOptions()
        {
            return tree.targetScript.GetClass()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.ReturnType == typeof(IEnumerator))
                .Select(m => m.Name)
                .ToArray();
        }
    }
}