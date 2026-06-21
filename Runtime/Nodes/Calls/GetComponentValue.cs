using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using Minerva.Module;
using UnityEngine;
using UnityEngine.Serialization;

namespace Aethiumian.AI.Nodes
{

    [NodeTip("Get value of a component on the attached game object")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class GetComponentValue : ObjectGetValueBase, IComponentCaller
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
            return GetValues(component);
        }

    }
}
