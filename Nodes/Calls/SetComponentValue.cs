﻿using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using UnityEngine;
using UnityEngine.Serialization;

namespace Amlos.AI.Nodes
{
    [NodeTip("Set value of a component on the attached game object")]
    public sealed class SetComponentValue : ObjectSetValueBase, IComponentCaller
    {
        public bool getComponent;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;
        [FormerlySerializedAs("componentReference")]
        public TypeReference<Component> type;

        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public VariableReference Component => component;
        public TypeReference TypeReference => type;

        public override State Execute()
        {
            var component = getComponent ? gameObject.GetComponent(type) : this.component.Value;
            return SetValues(component);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as SetComponentValue).fieldData = fieldData.DeepCloneToList();
            return result;
        }
    }
}