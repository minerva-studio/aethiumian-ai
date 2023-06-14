using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// base class of node that returns a boolean value to determine something happened
    /// <br/> 
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public abstract class DetermineBase : TreeNode
    {
        public bool storeResult;
        public abstract VariableReferenceBase Result { get; }


        public virtual Exception IsValidNode()
        {
            return null;
        }

        public override void Initialize()
        {
        }
    }

    /// <summary>
    /// determines nodes that the result can only be true or false
    /// <br/>
    /// See <see cref="ComparableDetermine{T}"/> for other type of determine
    /// </summary>
    public abstract class Determine : DetermineBase
    {
        public VariableReference<bool> result;
        public override sealed VariableReferenceBase Result => result;



        public abstract bool GetValue();



        public sealed override State Execute()
        {
            var e = IsValidNode();
            if (e != null) return HandleException(e);

            var value = GetValue();
            if (storeResult) StoreResult(value);
            return StateOf(value);
        }

        protected void StoreResult(bool result)
        {
            if (this.result.HasEditorReference) this.result.Value = result;
        }
    }
}