using Aethiumian.AI.Variables;
using System.Reflection;
using UnityEngine.Scripting.APIUpdating;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Updates an ordinary Float only while the host branch is active.</summary>
    [NodeTip("Counts elapsed time only while the host branch is active.")]
    [System.Serializable]
    [MovedFrom(true, "Aethiumian.AI.Nodes", "Aethiumian.AI", "BranchCountdown")]
    public class Countdown : Service
    {
        [Readable, Writable]
        public VariableReference<float> updatingVariable = new();

        private bool hasSample;
        private double lastTime;

        public override bool IsReady => true;

        public override State Execute() => State.Yield;

        public override void Initialize()
        {
            RuntimeVariable variable = updatingVariable?.RuntimeVariable;
            if (variable == null
                || variable is TimerVariable
                || variable.Type != VariableType.Float
                || !CanReadAndWrite(variable))
            {
                throw new System.InvalidOperationException("Countdown requires a readable and writable ordinary Float binding.");
            }

            hasSample = false;
            lastTime = 0d;
        }

        /// <summary>Starts sampling the owning tree timeline when the host branch becomes active.</summary>
        public override void OnRegistered()
        {
            lastTime = behaviourTree.Timer.Now;
            hasSample = true;
        }

        /// <summary>Stops sampling before the final variable write can invoke user code.</summary>
        public override void OnUnregistered()
        {
            if (!hasSample) return;
            hasSample = false;
            ApplyElapsedTime();
        }

        /// <summary>Applies active elapsed time during the existing service update cadence.</summary>
        public override void UpdateTimer()
        {
            if (!hasSample)
            {
                lastTime = behaviourTree.Timer.Now;
                hasSample = true;
                return;
            }

            ApplyElapsedTime();
        }

        private void ApplyElapsedTime()
        {
            double currentTime = behaviourTree.Timer.Now;
            double elapsed = currentTime - lastTime;
            lastTime = currentTime;
            if (elapsed <= 0d) return;

            float remaining = updatingVariable.FloatValue;
            if (float.IsNaN(remaining) || float.IsInfinity(remaining))
            {
                updatingVariable.SetValue(remaining);
                return;
            }

            float next = UnityEngine.Mathf.Max(0f, remaining - (float)UnityEngine.Mathf.Min((float)elapsed, float.MaxValue));
            updatingVariable.SetValue(next);
        }

        private static bool CanReadAndWrite(RuntimeVariable variable)
        {
            if (variable is TreeVariable) return true;
            if (variable is not TargetScriptVariable scriptVariable) return false;
            MemberInfo member = scriptVariable.Member;
            return member != null && VariableUtility.CanRead(member) && VariableUtility.CanWrite(member);
        }
    }
}
