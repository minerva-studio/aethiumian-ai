using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Set value of a component on the attached game object")]
    public sealed class SetComponentValue : Call
    {
        public bool getComponent;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;
        public TypeReference<Component> componentReference;
        public List<FieldChangeData> fieldData = new();

        public override void Execute()
        {
            Type type = componentReference;
            var component = getComponent ? gameObject.GetComponent(type) : this.component.Value;
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
                    fi.SetValue(component, item.data.GetValue(fi.FieldType));
                    //Debug.Log($"Change Entry {item.name} applied to component {type.Name}");
                }
                else if (member is PropertyInfo pi)
                {
                    pi.SetValue(component, item.data.GetValue(pi.PropertyType));
                    //Debug.Log($"Change Entry {item.name} applied to component {type.Name}");
                }
            }
            End(true);
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

        public bool IsChangeDefinded(string fieldName)
        {
            return fieldData.Any(f => f.name == fieldName);
        }

        public FieldChangeData GetChangeEntry(string fieldName)
        {
            return fieldData.FirstOrDefault(f => f.name == fieldName);
        }

        public void AddChangeEntry(string fieldName, VariableType type)
        {
            fieldData.Add(new FieldChangeData { name = fieldName, data = new VariableField(type) });
        }

        public void AddChangeEntry(string fieldName, object value)
        {
            fieldData.Add(new FieldChangeData { name = fieldName, data = new VariableField(value) });
        }

        public void RemoveChangeEntry(string fieldName)
        {
            fieldData.RemoveAll(f => f.name == fieldName);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as SetComponentValue).fieldData = fieldData.DeepCloneToList();
            return result;
        }
    }
}