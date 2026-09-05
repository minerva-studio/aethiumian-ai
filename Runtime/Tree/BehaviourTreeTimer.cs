using UnityEngine;

namespace Aethiumian.AI
{
    /// <summary>
    /// Owns the timeline for one root behaviour-tree lifetime. Subtrees share this
    /// instance; only the root tree changes its active state.
    /// </summary>
    internal sealed class BehaviourTreeTimer
    {
        private readonly TimeSettings settings;
        private double accumulatedTime;
        private double activeSince;
        private bool active;

        internal BehaviourTreeTimer(TimeSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>Gets the current value without changing timer lifecycle state.</summary>
        internal double Now
        {
            get
            {
                double now = ReadUnityTime(settings.scaleMode);
                if (settings.domain == TimeDomain.Game || !active)
                    return settings.domain == TimeDomain.Game ? now : accumulatedTime;

                return accumulatedTime + now - activeSince;
            }
        }

        /// <summary>Starts or settles the AI timeline only when its state changes.</summary>
        internal void SetActive(bool value)
        {
            if (settings.domain == TimeDomain.Game || value == active)
                return;

            double now = ReadUnityTime(settings.scaleMode);
            if (value)
            {
                activeSince = now;
                active = true;
                return;
            }

            accumulatedTime += now - activeSince;
            active = false;
        }

        private static double ReadUnityTime(TimeScaleMode mode)
        {
            return mode == TimeScaleMode.Unscaled
                ? Time.unscaledTimeAsDouble
                : Time.timeAsDouble;
        }
    }
}
