using Amlos.AI.Variables;
using System;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Base class for all comparable determine (i.e. value that can be compared, like <see cref="float"/>)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ComparableDetermine<T> : DetermineBase, IComparableDetermine
    {
        public bool compare = true;
        public CompareSign mode;
        public VariableField<T> expect;
        public VariableReference<T> result;
        public VariableReference<bool> compareResult;




        public bool CanPerformComparison => (T)default is IComparable;
        public bool Compare { get => compare; set => compare = value; }
        public CompareSign Mode { get => mode; set => mode = value; }
        public override sealed VariableReferenceBase Result => result;
        public VariableBase Expect => expect;
        public VariableReference<bool> CompareResult => compareResult;
        public virtual bool Yield => false;



        public abstract T GetValue();



        public sealed override State Execute()
        {
            if (Yield) return State.Yield;

            var value = GetValue();
            var result = !compare || CompareValue(value);

            if (storeResult)
            {
                StoreResult(value);
                if (compare) StoreCompareResult(result);
            }

            return StateOf(result);
        }

        protected bool CompareValue(T value)
        {
            if (CanPerformComparison) return ValueUtility.Compare(value as IComparable, (T)expect as IComparable, mode);
            else return ValueUtility.Equals(value, (T)expect, mode);
        }

        protected void StoreResult(T result)
        {
            if (this.result.HasEditorReference) this.result.Value = result;
        }

        protected void StoreCompareResult(bool compareResult)
        {
            if (this.compareResult.HasEditorReference) this.compareResult.Value = compareResult;
        }
    }

    public interface IComparableDetermine
    {
        bool Compare { get; set; }
        CompareSign Mode { get; set; }
        VariableReference<bool> CompareResult { get; }
        VariableBase Expect { get; }
        bool CanPerformComparison { get; }
    }
}