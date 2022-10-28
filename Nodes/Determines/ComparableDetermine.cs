using System;

namespace Amlos.AI
{
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
        public VariableFieldBase Expect => expect;
        public VariableReference<bool> CompareResult => compareResult;



        public abstract T GetValue();


        public sealed override void Execute()
        {
            var value = GetValue();
            var result = !compare || CompareValue(value);

            if (storeResult)
            {
                StoreResult(value);
                if (compare) StoreCompareResult(result);
            }

            End(result);
        }

        protected bool CompareValue(T value)
        {
            if (CanPerformComparison) return Amlos.AI.Compare.ValueCompare(value as IComparable, expect.Value as IComparable, mode);
            else return Amlos.AI.Equals.ValueEquals(value, expect.Value, mode.ToEqualityCheck());
        }

        protected void StoreResult(T result)
        {
            if (this.result.HasReference) this.result.Value = result;
        }

        protected void StoreCompareResult(bool compareResult)
        {
            if (this.compareResult.HasReference) this.compareResult.Value = compareResult;
        }
    }

    public interface IComparableDetermine
    {
        bool Compare { get; set; }
        CompareSign Mode { get; set; }
        VariableReference<bool> CompareResult { get; }
        VariableFieldBase Expect { get; }
        bool CanPerformComparison { get; }
    }
}