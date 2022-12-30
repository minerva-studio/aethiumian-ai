using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{

    [NodeTip("Get value of a component on the attached game object")]
    public sealed class GetComponentValue : ObjectGetValueBase, IComponentCaller
    {
        public bool getComponent;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;
        public TypeReference<Component> componentReference;

        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public VariableReference Component => component;
        public TypeReference TypeReference => componentReference;

        public override void Execute()
        {
            var component = getComponent ? gameObject.GetComponent(componentReference) : this.component.Value;
            GetValues(component);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as GetComponentValue).fieldPointers = fieldPointers.DeepCloneToList();
            return result;
        }
    }
}