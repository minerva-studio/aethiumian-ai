using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        [System.NonSerialized] private Vector2? wanderDestination;

        /// <summary>
        /// Gets the destination sampled for the current run, in this capability's planning frame.
        /// It is null until a Wander goal has been sampled.
        /// </summary>
        protected Vector2? WanderDestination => wanderDestination;

        private bool TryReadTarget(AABB body, out AABB target, out GameObject targetObject)
        {
            Vector2 point;
            target = default;
            targetObject = null;
            switch (type)
            {
                case Behaviour.Trace:
                case Behaviour.Retreat:
                    {
                        targetObject = capturedTarget;
                        if (!targetObject) return false;
                        Collider2D[] colliders = NavigationBodyGeometry.GetTargetColliders(targetObject);
                        Vector2 fallback = targetObject.transform.position;
                        target = colliders.Length > 0 ? NavigationBodyGeometry.GetMergedAabb(colliders) : AABB.Point(fallback);
                        return true;
                    }
                case Behaviour.Wander:
                    {
                        var center = wanderMode switch
                        {
                            WanderMode.SelfCentered => body.LowerCenter,
                            WanderMode.AbsoluteCentered when centerSpace == Space.World => centerOfWander.Vector2Value,
                            WanderMode.AbsoluteCentered when centerSpace == Space.Self => centerOfWander.Vector2Value + body.LowerCenter,
                            _ => Vector2.zero,
                        };
                        wanderDestination ??= GetWanderLocation(center, body);
                        point = wanderDestination.Value;
                        break;
                    }
                case Behaviour.FixedDestination:
                    {
                        point = destination.Vector2Value;
                        break;
                    }
                default:
                    {
                        point = body.LowerCenter;
                        break;
                    }
            }
            target = AABB.Point(point);
            return true;
        }

        protected NavigationGoalRequest CreateGoal(AABB target, NavigationGoalGeometry defaultGeometry)
        {
            float tolerance = reachDistance;
            if (type == Behaviour.Retreat) return NavigationGoalRequest.Retreat(target, distanceMetric, tolerance);
            if (type == Behaviour.Wander && NavigationNumeric.IsFinite(tolerance)) tolerance = Mathf.Max(0f, tolerance);
            bool ground = goal == MovementGoal.Confront || goal == MovementGoal.Default && defaultGeometry == NavigationGoalGeometry.GroundRange;
            bool sight = goal == MovementGoal.Confront || goal == MovementGoal.FiringPosition;
            return ground ? NavigationGoalRequest.GroundRange(target, tolerance, sight)
                : NavigationGoalRequest.Proximity(target, distanceMetric, tolerance, sight);
        }

        /// <summary>
        /// Chooses a destination once per run, using the ability's valid landing geometry.
        /// </summary>
        protected abstract Vector2 GetWanderLocation(Vector2 center, AABB body);

        /// <summary>
        /// Validates one sampled Wander candidate. Region membership uses the sampled body's center as the
        /// origin, the same canonical origin every other destination check uses; the destination is the
        /// sampled point in the capability's own frame (ground lower center or aerial body center), passed
        /// unchanged so no anchor is re-derived from the candidate body.
        /// </summary>
        protected bool IsWanderCandidateAllowed(Vector2 origin, Vector2 destination, AABB candidateBody, bool requireSupport)
        {
            INavigationWorld world = NavigationWorld;
            if (world == null) return true;
            if (!IsNavigationDestinationAllowed(origin, destination)) return false;
            if (!world.IsBodyClear(candidateBody, 0f)) return false;
            if (!requireSupport) return true;

            return world.TryResolveSupport(
                candidateBody,
                NavigationWorldQueries.SupportSnapDistance,
                out _);
        }
    }
}
