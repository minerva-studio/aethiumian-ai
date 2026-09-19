using System;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Provides shared physical tolerances for ground traversal endpoint decisions.</summary>
    public static class GroundTraversalEndpointPolicy
    {
        internal const float MinimumHorizontalCompletionTolerance = NavigationConstant.ArrivalFloor;

        private static float verticalSupportTolerance;
        private static int verticalSupportToleranceCaptured;

        /// <summary>
        /// Gets the vertical tolerance captured from the Unity physics policy.
        /// The cached managed value keeps background planning from reading Unity physics state.
        /// </summary>
        public static float VerticalSupportTolerance
        {
            get
            {
                CaptureVerticalSupportTolerance();
                return verticalSupportTolerance;
            }
        }

        /// <summary>Captures Unity physics policy on the main-thread owner boundary.</summary>
        internal static void CaptureVerticalSupportTolerance()
        {
            if (Volatile.Read(ref verticalSupportToleranceCaptured) != 0) return;
            verticalSupportTolerance = NavigationConstant.SupportResidual;
            Volatile.Write(ref verticalSupportToleranceCaptured, 1);
        }

        /// <summary>Gets the physical horizontal tolerance for a completed traversal endpoint.</summary>
        public static float GetHorizontalCompletionTolerance(float horizontalSpeed) => GetHorizontalCompletionTolerance(horizontalSpeed, Time.fixedDeltaTime);

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
