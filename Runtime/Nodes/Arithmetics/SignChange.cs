using Amlos.AI.Variables;

namespace Amlos.AI.Nodes
{
    [NodeTip("Check sign of the value changed, given an error bound")]
    public sealed class SignChange : Arithmetic
    {
        public enum Determine
        {
            isPositive,
            isNegative,
        }

        public float bound = 0.1f;
        public Determine determine;
        [Numeric]
        public VariableReference value;
        public VariableReference<bool> baseValue;
        public VariableReference<bool> change;

        public override bool EditorCheck(BehaviourTreeData tree)
        {
            return value.HasEditorReference
                || baseValue.HasEditorReference
                || change.HasEditorReference;
        }

        public override State Execute()
        {
            float value = this.value.NumericValue;
            if (value < -bound)
            {
                change.Value = determine == Determine.isNegative;
                return State.Success;
            }
            else if (value > bound)
            {
                change.Value = determine == Determine.isPositive;
                return State.Success;
            }
            // not change
            else
            {
                bool boolValue = baseValue.BoolValue;
                change.Value = boolValue;
                return State.Failed;
            }
        }
    }
}
