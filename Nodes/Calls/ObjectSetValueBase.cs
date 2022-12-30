using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI
{
    /// <summary>
    /// Base class for set object value
    /// </summary>
    public abstract class ObjectSetValueBase : Call
    {
        public List<FieldChangeData> fieldData = new();

        public FieldChangeData AddChangeEntry(string fieldName, VariableType type)
        {
            FieldChangeData item = new() { name = fieldName, data = new Parameter(type) };
            fieldData.Add(item);
            return item;
        }

        public FieldChangeData AddChangeEntry(string fieldName, object value)
        {
            FieldChangeData item = new() { name = fieldName, data = new Parameter(value) };
            fieldData.Add(item);
            return item;
        }

        public FieldChangeData GetChangeEntry(string fieldName)
        {
            return fieldData.FirstOrDefault(f => f.name == fieldName);
        }

        public override void Initialize()
        {
            for (int i = fieldData.Count - 1; i >= 0; i--)
            {
                var item = fieldData[i];
                if (!item.data.IsConstant)
                {
                    if (behaviourTree.GetVariable(item.data.UUID, out var variable))
                        item.data.SetRuntimeReference(variable);
                    else fieldData.RemoveAt(i);
                }
            }
        }

        public bool IsEntryDefinded(string fieldName)
        {
            return fieldData.Any(f => f.name == fieldName);
        }

        public void RemoveChangeEntry(string fieldName)
        {
            fieldData.RemoveAll(f => f.name == fieldName);
        }

        protected void SetValues(object obj)
        {
            Type type = obj.GetType();
            foreach (var item in fieldData)
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
                    fi.SetValue(obj, item.data.GetValue(fi.FieldType));
                    //Debug.Log($"Change Entry {item.name} applied to component {type.Name}");
                }
                else if (member is PropertyInfo pi)
                {
                    pi.SetValue(obj, item.data.GetValue(pi.PropertyType));
                    //Debug.Log($"Change Entry {item.name} applied to component {type.Name}");
                }
            }
            End(true);
        }
    }
}