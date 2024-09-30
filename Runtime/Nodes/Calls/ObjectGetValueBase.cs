using Amlos.AI.Utils;
using Amlos.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Amlos.AI.Nodes
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

                if (behaviourTree.TryGetVariable(item.data.UUID, out var variable))
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

        protected State GetValues(object obj)
        {
            foreach (var item in fieldPointers)
            {
                if (!item.data.HasReference) continue;
                var member = MemberInfoCache.Instance.GetMember(obj, item.name);
                //Debug.Log($"Found");
                switch (member)
                {
                    case FieldInfo fi:
                        item.data.SetValue(fi.GetValue(obj));
                        break;
                    case PropertyInfo pi:
                        item.data.SetValue(pi.GetValue(obj));
                        //Debug.Log($"Get Entry {item.name} to var {item.data.UUID}");
                        break;
                    default:
                        return State.Failed;
                }
            }
            return State.Success;
        }

    }
}