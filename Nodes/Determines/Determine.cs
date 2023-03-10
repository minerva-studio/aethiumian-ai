using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// base class of node that returns a boolean value to determine something happened
    /// <br></br> 
    /// </summary>
    [Serializable]
    [AllowServiceCall]
    public abstract class DetermineBase : TreeNode
    {
        public bool storeResult;
        public abstract VariableReferenceBase Result { get; }


        public override void Initialize()
        {
        }
    }


    public abstract class Determine : DetermineBase
    {
        public VariableReference<bool> result;
        public override sealed VariableReferenceBase Result => result;



        public abstract bool GetValue();



        public sealed override void Execute()
        {
            var value = GetValue();
            if (storeResult) StoreResult(value);
            End(value);
        }

        protected void StoreResult(bool result)
        {
            if (this.result.HasEditorReference) this.result.Value = result;
        }
    }
}