using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        /// <summary>Creates the goal provider for one Action execution.</summary>
        protected virtual MovementGoalProvider CreateGoalProvider() => type switch
        {
            Behaviour.Trace => new TraceGoalProvider(this),
            Behaviour.Wander => new WanderGoalProvider(this),
            Behaviour.FixedDestination => new FixedDestinationGoalProvider(this),
            Behaviour.Retreat => new RetreatGoalProvider(this),
            _ => new FallbackGoalProvider(this),
        };

        /// <summary>
        /// Supplies and validates the target for one Movement execution. Providers do not
        /// choose planners, advance physical actions, or own execution settlement state.
        /// </summary>
        protected abstract class MovementGoalProvider
        {
            protected readonly Movement Owner;

            protected MovementGoalProvider(Movement owner) => Owner = owner;

            public virtual void Initialize() { }
            public abstract Vector2 GetDestination();
            public abstract NavigationGoalRequest CreateGoalRequest();
            public virtual bool ValidateTarget() => true;

            protected NavigationGoalRequest CreateApproachGoal(Bounds bounds, float arrivalTolerance)
            {
                bool groundRange = Owner.goal == MovementGoal.Confront
                    || Owner.goal == MovementGoal.Default
                    && Owner.DefaultGoalGeometry == NavigationGoalGeometry.GroundRange;
                bool requiresLineOfSight = Owner.goal == MovementGoal.Confront
                    || Owner.goal == MovementGoal.FiringPosition;
                return groundRange
                    ? NavigationGoalRequest.GroundRange(bounds, arrivalTolerance, requiresLineOfSight)
                    : NavigationGoalRequest.Proximity(bounds, Owner.distanceMetric, arrivalTolerance, requiresLineOfSight);
            }

            protected Bounds GetTracingBounds()
            {
                Bounds bounds = new Bounds(GetDestination(), Vector3.zero);
                Collider2D[] targetColliders = NavigationBodyGeometry.GetTargetColliders(
                    Owner.tracing.GameObjectValue);
                if (targetColliders.Length > 0)
                    bounds = NavigationBodyGeometry.GetMergedBounds(targetColliders);
                return bounds;
            }
        }

        private sealed class TraceGoalProvider : MovementGoalProvider
        {
            public TraceGoalProvider(Movement owner) : base(owner) { }

            public override void Initialize()
            {
                if (!ValidateTarget()) Owner.CompleteAction(false);
            }

            public override Vector2 GetDestination() => Owner.tracing.PositionValue;

            public override NavigationGoalRequest CreateGoalRequest() => CreateApproachGoal(GetTracingBounds(), Owner.reachDistance.NumericValue);

            public override bool ValidateTarget()
                => Owner.tracing != null
                    && Owner.tracing.HasValue
                    && !Owner.tracing.IsNull
                    && Owner.tracing.GameObjectValue;
        }

        private sealed class FixedDestinationGoalProvider : MovementGoalProvider
        {
            public FixedDestinationGoalProvider(Movement owner) : base(owner) { }

            public override Vector2 GetDestination() => Owner.destination.Vector2Value;

            public override NavigationGoalRequest CreateGoalRequest() => CreateApproachGoal(new Bounds(GetDestination(), Vector3.zero), Owner.reachDistance.NumericValue);
        }

        private sealed class WanderGoalProvider : MovementGoalProvider
        {
            private Vector2Int wanderPosition;

            public WanderGoalProvider(Movement owner) : base(owner) { }

            public override void Initialize() => wanderPosition = Owner.GetWanderLocation(GetCenter());

            public override Vector2 GetDestination() => wanderPosition;

            public override NavigationGoalRequest CreateGoalRequest()
            {
                float arrivalTolerance = Owner.reachDistance.NumericValue;
                if (NavigationNumeric.IsFinite(arrivalTolerance))
                    arrivalTolerance = Mathf.Max(0f, arrivalTolerance);
                return CreateApproachGoal(new Bounds(GetDestination(), Vector3.zero), arrivalTolerance);
            }

            private Vector2 GetCenter()
            {
                switch (Owner.wanderMode)
                {
                    case WanderMode.SelfCentered:
                        return Owner.NavigationGroundAnchor;
                    case WanderMode.AbsoluteCentered:
                        switch (Owner.centerSpace)
                        {
                            case Space.World:
                                return Owner.centerOfWander.Vector2Value;
                            case Space.Self:
                                return Owner.centerOfWander.Vector2Value + Owner.NavigationGroundAnchor;
                        }
                        break;
                }

                return Vector2.zero;
            }
        }

        // Preserve the former switch fallback for unknown serialized enum values.
        private sealed class FallbackGoalProvider : MovementGoalProvider
        {
            public FallbackGoalProvider(Movement owner) : base(owner) { }

            public override Vector2 GetDestination() => Owner.NavigationGroundAnchor;

            public override NavigationGoalRequest CreateGoalRequest() => CreateApproachGoal(new Bounds(GetDestination(), Vector3.zero), Owner.reachDistance.NumericValue);
        }

        private sealed class RetreatGoalProvider : MovementGoalProvider
        {
            public RetreatGoalProvider(Movement owner) : base(owner) { }

            public override void Initialize()
            {
                if (!ValidateTarget()
                    || Owner.reachDistance == null
                    || !Owner.reachDistance.HasValue
                    || !NavigationNumeric.IsFinite(Owner.reachDistance.NumericValue)
                    || Owner.reachDistance.NumericValue < 0f)
                    Owner.CompleteAction(false);
            }

            public override Vector2 GetDestination() => Owner.tracing.PositionValue;

            public override NavigationGoalRequest CreateGoalRequest() => NavigationGoalRequest.Retreat(GetTracingBounds(), Owner.distanceMetric, Owner.reachDistance.NumericValue);

            public override bool ValidateTarget()
                => Owner.tracing != null
                    && Owner.tracing.HasValue
                    && !Owner.tracing.IsNull
                    && Owner.tracing.GameObjectValue;
        }
    }
}
