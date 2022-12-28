using System;
using UnityEditor;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(StaticCall))]
    public class StaticCallDrawer : MethodCallerDrawerBase
    {
        public override void Draw()
        {
            if (node is not StaticCall call) return;
            if (!DrawReferType(call)) return;

            var method = methods.FirstOrDefault(m => m.Name == call.MethodName);
            if (method is null)
            {
                EditorGUILayout.LabelField("Cannot load method info");
                return;
            }

            DrawParameters(call, method);
            DrawResultField(call.result, method);
        }

        //public bool TypeFilter(Type type)
        //{
        //    return type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy).Length != 0;
        //}
    }
}