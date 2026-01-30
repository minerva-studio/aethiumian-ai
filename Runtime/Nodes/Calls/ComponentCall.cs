using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;

namespace Amlos.AI.Nodes
{
    [DoNotRelease]
    [Serializable]
    public sealed class ComponentCall : ObjectCallBase, IGenericMethodCaller, IComponentMethodCaller
    {
        public bool getComponent = true;
        public GenericTypeReference type;
        [DisplayIf(nameof(getComponent), false)] public VariableReference component;


        public bool GetComponent { get => getComponent; set => getComponent = value; }
        public TypeReference TypeReference => type;
        public VariableReference Component { get => component; set => component = value; }

        public override State Execute()
        {
            Type referType = type.ReferType;
            var obj = getComponent ? gameObject.GetComponent(referType) : this.component.Value;
            return Call(obj, referType);
        }
    }
}
