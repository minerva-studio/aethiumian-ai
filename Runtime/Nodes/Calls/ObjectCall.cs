using Amlos.AI.References;
using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    [NodeMenuPath("External")]
    [NodeTip("Call a method on the object")]
    [Serializable]
    public sealed class ObjectCall : ObjectCallBase, IGenericMethodCaller, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference type;


        public TypeReference TypeReference => type;
        public VariableReference Object { get => @object; set => @object = value; }

        public override State Execute()
        {
            Type referType = type.ReferType;
            object obj = @object.Value;
            return Call(obj, referType);
        }
    }
}
