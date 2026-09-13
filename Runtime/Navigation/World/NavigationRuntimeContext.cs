using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Holds the project-selected runtime for new AI navigation executions.</summary>
    /// <remarks>
    /// Access is restricted to the Unity main thread. The project owns the runtime's lifetime;
    /// changing this borrowed reference neither disposes the old runtime nor rebinds active executions.
    /// </remarks>
    public static class NavigationRuntimeContext
    {
        /// <summary>Gets the borrowed runtime, or null when the project has not supplied one.</summary>
        public static MapNavigationRuntime Current { get; private set; }

        /// <summary>Selects a live runtime; its World need not have been published yet.</summary>
        /// <exception cref="ArgumentNullException">The runtime is null.</exception>
        /// <exception cref="ObjectDisposedException">The runtime has already been disposed.</exception>
        public static void SetCurrent(MapNavigationRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            if (runtime.IsDisposed) throw new ObjectDisposedException(nameof(runtime));
            Current = runtime;
        }

        /// <summary>Clears only the expected reference, so an old owner cannot clear its replacement.</summary>
        public static void ClearCurrent(MapNavigationRuntime expectedRuntime)
        {
            if (ReferenceEquals(Current, expectedRuntime)) Current = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCurrent()
        {
            Current = null;
        }
    }
}
