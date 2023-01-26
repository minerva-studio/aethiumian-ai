using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Get a component")]
    [Serializable]
    public sealed class GetComponent : Call
    {
        public enum Mode
        {
            [Tooltip("Get Component on self")]
            Self,
            [Tooltip("Get Component on parent")]
            Parent,
            [Tooltip("Get Component on children")]
            Children,
        }

        public Mode getMode;
        public bool getMultiple;
        [DisplayIf(nameof(getMode), false, Mode.Self)]
        public VariableField<bool> includeInactive;
        public TypeReference<Component> type;
        public VariableReference result;

        public override void Execute()
        {
            if (getMultiple) GetMultipleComponent();
            else GetSingleComponent();

        }

        private void GetSingleComponent()
        {
            Component component = getMode switch
            {
                Mode.Parent => gameObject.GetComponentInParent(type.ReferType, includeInactive),
                Mode.Children => gameObject.GetComponentInChildren(type.ReferType, includeInactive),
                _ => gameObject.GetComponent(type.ReferType),
            };
            if (result.HasValue) result.Value = component;
            //Debug.Log(type.ReferType?.Name);
            //Debug.Log(component);
            End(component);
        }

        private void GetMultipleComponent()
        {
            var components = getMode switch
            {
                Mode.Parent => gameObject.GetComponentsInParent(type.ReferType, includeInactive),
                Mode.Children => gameObject.GetComponentsInChildren(type.ReferType, includeInactive),
                _ => gameObject.GetComponents(type.ReferType),
            };
            if (result.HasValue) result.Value = components;
            End(components.Length != 0);
            return;
        }
    }
}