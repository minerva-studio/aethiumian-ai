using Minerva.Module;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Call a method on the object")]
    [Serializable]
    public sealed class ObjectCall : ObjectCallBase, IGenericMethodCaller, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference<Component> type;


        public TypeReference TypeReference { get => type; }
        public VariableReference Object { get => @object; set => @object = value; }

        public override void Execute()
        {
            Type referType = type.ReferType;
            object obj = @object.Value;
            Call(obj, referType);
        }
    }
}