using Minerva.Module;
using UnityEngine;

namespace Amlos.AI
{
    [NodeTip("Set value of a object")]
    public sealed class SetObjectValue : ObjectSetValueBase, IObjectCaller
    {
        public VariableReference @object;
        public TypeReference<Component> type;

        public VariableReference Object => @object;
        public TypeReference TypeReference => type;

        public override void Execute()
        {
            object component = @object.Value;
            SetValues(component);
        }

        public override TreeNode Clone()
        {
            var result = base.Clone();
            (result as SetObjectValue).fieldData = fieldData.DeepCloneToList();
            return result;
        }
    }
}