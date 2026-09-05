using System;

namespace Aethiumian.AI
{
    /// <summary>Selects the lifetime that supplies elapsed time to a timer.</summary>
    public enum TimeDomain
    {
        Game = 0,
        AI = 1,
    }

    /// <summary>Selects whether a timer observes scaled or unscaled Unity time.</summary>
    public enum TimeScaleMode
    {
        Scaled = 0,
        Unscaled = 1,
    }

    /// <summary>Behaviour-tree timeline selection. Zero values are the default Game + Scaled behavior.</summary>
    [Serializable]
    public struct TimeSettings
    {
        public TimeDomain domain;
        public TimeScaleMode scaleMode;

        public TimeDomain Domain => domain;
        public TimeScaleMode ScaleMode => scaleMode;
    }
}
