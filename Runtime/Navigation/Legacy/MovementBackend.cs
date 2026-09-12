namespace Aethiumian.AI.Navigation
{
    /// <summary>Provides the temporary process-wide movement backend used during development migration.</summary>
    public static class MovementBackend
    {
        /// <summary>Identifies the movement implementation selected for newly started movement nodes.</summary>
        public enum Mode
        {
            /// <summary>Uses the existing node-specific movement implementation.</summary>
            Legacy,

            /// <summary>Uses the new shared movement implementation.</summary>
            New,
        }

        /// <summary>Gets or sets the backend used by newly started movement nodes.</summary>
        public static Mode Current { get; set; } = Mode.New;
    }
}
