using Amlos.AI.Variables;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Updaing an value, like a timer
    /// </summary>
    [NodeTip("Updaing an value, like a timer")]
    public class Timer : Service
    {
        public VariableReference<float> updatingVariable;
        public Timing timing;

        public enum Timing
        {
            FixedDeltaTime,
            FixedUnscaledDeltaTime,
            //DeltaTime,
            //SmoothedDeltaTime,
            //UnscaledDeltaTime,
        }


        public override bool IsReady => true;

        public override State Execute()
        {
            return State.Yield;
        }

        private float GetDt()
        {
            switch (timing)
            {
                case Timing.FixedDeltaTime:
                    return Time.fixedDeltaTime;
                case Timing.FixedUnscaledDeltaTime:
                    return Time.fixedUnscaledDeltaTime;
                    //case Timing.DeltaTime:
                    //    return Time.deltaTime;
                    //case Timing.SmoothedDeltaTime:
                    //    return Time.smoothDeltaTime;
                    //case Timing.UnscaledDeltaTime:
                    //    return Time.unscaledDeltaTime;
            }
            return 0;
        }

        public override void Initialize()
        {
            // nothing, variable will be initialized by construction
        }

        public override void UpdateTimer()
        {
            // nothing
            updatingVariable.SetValue(updatingVariable.FloatValue - GetDt());
        }
    }
}
