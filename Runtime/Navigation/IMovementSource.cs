namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Describes the locomotion state explicitly reported by a movement action.
    /// </summary>
    public enum MovementState
    {
        /// <summary>No valid locomotion state has been reported.</summary>
        Unspecified = 0,
        /// <summary>The movement owner explicitly reports no active locomotion.</summary>
        Idle = 1,
        Walking = 2,
        Jumping = 3,
        Falling = 4,
        DroppingThrough = 5,
        Flying = 6,
        Sprinting = 7,
    }

    /// <summary>Provides one extensible movement-state report.</summary>
    public readonly struct MovementStateInfo
    {
        public MovementStateInfo(MovementState state)
        {
            State = state;
        }

        /// <summary>Gets the explicitly reported locomotion state.</summary>
        public MovementState State { get; }
    }

    /// <summary>Describes one accepted movement-state transition.</summary>
    public readonly struct MovementStateChange
    {
        public MovementStateChange(MovementStateInfo previous, MovementStateInfo current)
        {
            Previous = previous;
            Current = current;
        }

        /// <summary>Gets the state before the transition.</summary>
        public MovementStateInfo Previous { get; }

        /// <summary>Gets the state after the transition.</summary>
        public MovementStateInfo Current { get; }
    }

    /// <summary>
    /// Exposes whether an AI control target may issue intentional movement.
    /// </summary>
    public interface IMovementSource
    {
        /// <summary>
        /// Gets whether intentional movement is currently permitted.
        /// </summary>
        bool CanMove { get; }

        /// <summary>
        /// Receives an explicit locomotion state report. Implementers that do not observe
        /// locomotion may keep the default no-op implementation.
        /// </summary>
        void SetMovementState(MovementStateInfo stateInfo) { }
    }
}
