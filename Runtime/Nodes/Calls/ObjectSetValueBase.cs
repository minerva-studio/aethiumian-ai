using Amlos.AI.Utils;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Nodes
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

        public FieldChangeData AddChangeEntry(string fieldName, Type restrictedType)
        {
            FieldChangeData item = new() { name = fieldName, data = new Parameter(restrictedType) };
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
                if (item.data.IsConstant) continue;

                if (behaviourTree.TryGetVariable(item.data.UUID, out var variable))
                    item.data.SetRuntimeReference(variable);
                else fieldData.RemoveAt(i);
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

        protected State SetValues(object obj)
        {
            foreach (var item in fieldData)
            {
                if (!item.data.HasValue) continue;
                var member = MemberInfoCache.Instance.GetMember(obj, item.name);
                switch (member)
                {
                    case FieldInfo fi:
                        fi.SetValue(obj, item.data.GetValue(fi.FieldType));
                        break;
                    case PropertyInfo pi:
                        pi.SetValue(obj, item.data.GetValue(pi.PropertyType));
                        break;
                    default:
                        DebugPrint.Log(item.name + "is not found");
                        break;
                }
            }
            return State.Success;
        }
    }
}