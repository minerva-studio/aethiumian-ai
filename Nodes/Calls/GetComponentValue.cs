using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Get value of a component on the attached game object")]
    public sealed class GetComponentValue : Call
    {
        public ComponentReference componentReference;
        public List<FieldPointer> fieldPointers = new();

        public override void Execute()
        {
            Type type = componentReference;
            Component component = gameObject.GetComponent(type);
            foreach (var item in fieldPointers)
            {
                //Debug.Log($"Loop");
                MemberInfo[] memberInfos = type.GetMember(item.name);
                if (memberInfos.Length == 0)
                {
                    //Debug.Log($"Change Entry {item.name} cannot apply to component {type.Name}");
                    continue;
                }
                var member = memberInfos[0];
                //Debug.Log($"Found");
                if (member is FieldInfo fi)
                {
                    item.data.Value = fi.GetValue(component);
                    //Debug.Log($"Get Entry {item.name} to var {item.data.UUID}");
                }
                else if (member is PropertyInfo pi)
                {
                    item.data.Value = pi.GetValue(component);
                    //Debug.Log($"Get Entry {item.name} to var {item.data.UUID}");
                }
            }
            End(true);
        }

        public override void Initialize()
        {
            foreach (var item in fieldPointers)
            {
                if (!item.data.IsConstant)
                {
                    item.data.SetRuntimeReference(behaviourTree.GetVariable(item.data.UUID));
                }
            }
        }

        public bool IsChangeDefinded(string fieldName)
        {
            return fieldPointers.Any(f => f.name == fieldName);
        }

        public FieldPointer GetChangeEntry(string fieldName)
        {
            return fieldPointers.FirstOrDefault(f => f.name == fieldName);
        }

        public void AddPointer(string fieldName, VariableType type)
        {
            fieldPointers.Add(new FieldPointer { name = fieldName, data = new VariableReference() { type = type } });
        }

        public void RemoveChangeEntry(string fieldName)
        {
            fieldPointers.RemoveAll(f => f.name == fieldName);
        }
    }
}