using Minerva.Module;
using System.Security.Cryptography;
using System;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Editor
{
    [CustomNodeDrawer(typeof(StaticCall))]
    public class StaticCallDrawer : ScriptMethodDrawerBase
    {
        private Type refType;
        private MethodInfo[] methods;
        TypeReferenceDrawer typeReferenceDrawer;

        public override void Draw()
        {
            if (node is not StaticCall call) return;
            typeReferenceDrawer = DrawType("Type", call.type, typeReferenceDrawer);
            if (call.type.ReferType == null)
            {
                EditorGUILayout.LabelField("Cannot load type");
                return;
            }
            if (call.type.ReferType != refType)
            {
                methods = call.type.ReferType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                refType = call.type.ReferType;
            }
            var names = methods.Where(m => IsValidMethod(m)).Select(s => s.Name).Where(s => !s.StartsWith("op_")).ToArray();
            int index = Array.IndexOf(names, call.MethodName);
            if (index < 0) index = 0;

            if (names.Length == 0)
            {
                EditorGUILayout.LabelField("No method found");
                return;
            }
            index = EditorGUILayout.Popup("Method Name", index, names);
            call.MethodName = names.Length > 0 ? names[index] : "";

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