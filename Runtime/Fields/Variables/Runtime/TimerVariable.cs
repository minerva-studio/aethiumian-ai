using System;
namespace Aethiumian.AI.Variables
{
    /// <summary>Runtime variable that exposes one authored-clock deadline as remaining seconds.</summary>
    [Serializable]
    public sealed class TimerVariable : RuntimeVariable
    {
        private double deadline;
        private readonly BehaviourTreeTimer timer;

        internal TimerVariable(BehaviourTreeTimer timer, UUID uuid, string name) : base(uuid, name)
        {
            this.timer = timer ?? throw new ArgumentNullException(nameof(timer));
        }

        /// <summary>Gets the non-negative remaining seconds.</summary>
        public override object Value => Remaining;

        /// <inheritdoc />
        public override VariableType Type => VariableType.Float;

        /// <inheritdoc />
        public override Type ObjectType => typeof(float);

        /// <summary>Gets the current remaining seconds without advancing an AI node.</summary>
        public float Remaining
        {
            get
            {
                if (deadline == 0d) return 0f;
                double remaining = deadline - timer.Now;
                if (remaining <= 0d) return 0f;
                return (float)Math.Min(remaining, float.MaxValue);
            }
        }

        /// <inheritdoc />
        public override T GetValue<T>() => VariableUtility.ImplicitConversion<T>(Remaining);

        /// <inheritdoc />
        public override void SetValue<T>(T value)
        {
            float seconds = VariableUtility.ImplicitConversion<float, T>(value);
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Timer duration must be finite.");
            }

            deadline = seconds <= 0f ? 0d : timer.Now + seconds;
        }
    }
}
