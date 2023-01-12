using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    public abstract class ObjectGetValueBase : Call
    {
        public List<FieldPointer> fieldPointers = new();

        public void AddPointer(string fieldName, VariableType type)
        {
            fieldPointers.Add(new FieldPointer { name = fieldName, data = new VariableReference() { type = type } });
        }

        public FieldPointer GetChangeEntry(string fieldName)
        {
            return fieldPointers.FirstOrDefault(f => f.name == fieldName);
        }

        public override void Initialize()
        {
            for (int i = fieldPointers.Count - 1; i >= 0; i--)
            {
                FieldPointer item = fieldPointers[i];

                if (item.data.IsConstant) continue;
                if (!item.data.HasReference) continue;

                if (behaviourTree.Variables.TryGetValue(item.data.UUID, out var variable))
                    item.data.SetRuntimeReference(variable);
                else if (BehaviourTree.GlobalVariables.TryGetValue(item.data.UUID, out variable))
                    item.data.SetRuntimeReference(variable);
                else fieldPointers.RemoveAt(i);
            }
        }

        public bool IsEntryDefinded(string fieldName)
        {
            return fieldPointers.Any(f => f.name == fieldName);
        }

        public void RemoveChangeEntry(string fieldName)
        {
            fieldPointers.RemoveAll(f => f.name == fieldName);
        }

        protected void GetValues(object obj)
        {
            Type type = obj.GetType();
            foreach (var item in fieldPointers)
            {
                if (!item.data.HasReference) continue;

                MemberInfo[] memberInfos = type.GetMember(item.name);
                if (memberInfos.Length == 0) continue;

                var member = memberInfos[0];
                //Debug.Log($"Found");
                if (member is FieldInfo fi)
                {
                    item.data.Value = fi.GetValue(obj);
                }
                else if (member is PropertyInfo pi)
                {
                    item.data.Value = pi.GetValue(obj);
                    //Debug.Log($"Get Entry {item.name} to var {item.data.UUID}");
                }
            }
            End(true);
        }

    }
}