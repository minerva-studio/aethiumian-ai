using System;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Provides shared physical tolerances for ground traversal endpoint decisions.</summary>
    public static class GroundTraversalEndpointPolicy
    {
        internal const float MinimumHorizontalCompletionTolerance = 0.2f;

        /// <summary>Gets the vertical tolerance used when resolving physical support.</summary>
        public static float VerticalSupportTolerance => Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon;

        /// <summary>Gets the physical horizontal tolerance for a completed traversal endpoint.</summary>
        public static float GetHorizontalCompletionTolerance(float horizontalSpeed, float simulationTimeStep)
        {
            ValidateInputs(horizontalSpeed, simulationTimeStep);
            return Mathf.Max(MinimumHorizontalCompletionTolerance,
                horizontalSpeed * simulationTimeStep + VerticalSupportTolerance);
        }

        /// <summary>Gets the one-step tolerance for a non-terminal traversal phase transition.</summary>
        public static float GetHorizontalTransitionTolerance(float horizontalSpeed, float simulationTimeStep)
        {
            ValidateInputs(horizontalSpeed, simulationTimeStep);
            return horizontalSpeed * simulationTimeStep + VerticalSupportTolerance;
        }

        private static void ValidateInputs(float horizontalSpeed, float simulationTimeStep)
        {
            if (!NavigationNumeric.IsFinite(horizontalSpeed) || horizontalSpeed < 0f)
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed));
            if (!NavigationNumeric.IsFinite(simulationTimeStep) || simulationTimeStep < 0f)
                throw new ArgumentOutOfRangeException(nameof(simulationTimeStep));
        }
    }
}
