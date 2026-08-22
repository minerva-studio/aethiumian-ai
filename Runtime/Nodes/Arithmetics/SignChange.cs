using Aethiumian.AI.Variables;

namespace Aethiumian.AI.Nodes
{
    [NodeTip("Check sign of the value changed, given an error bound")]
    [System.Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Amlos.AI.Nodes", "Aethiumian-AI")]
    public sealed class SignChange : Arithmetic
    {
        public enum Determine
        {
            isPositive,
            isNegative,
        }

        public float bound = 0.1f;
        public Determine determine;
        [Readable]
        [Numeric]
        public VariableReference value;
        [Readable]
        public VariableReference<bool> baseValue;

        [Writable]
        public VariableReference<bool> change;

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            return value.HasEditorReference
                || baseValue.HasEditorReference
                || change.HasEditorReference;
        }

        public override State Execute()
        {
            if (HasNaN(this.value))
            {
                return State.Failed;
            }

            float value = this.value.FloatValue;
            if (value < -bound)
            {
                change.SetValue(determine == Determine.isNegative);
                return State.Success;
            }
            else if (value > bound)
            {
                change.SetValue(determine == Determine.isPositive);
                return State.Success;
            }
            // not change
            else
            {
                bool boolValue = baseValue.BoolValue;
                change.SetValue(boolValue);
                return State.Failed;
            }
        }
    }
}
