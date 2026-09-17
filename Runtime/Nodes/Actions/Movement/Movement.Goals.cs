using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    public abstract partial class Movement
    {
        [System.NonSerialized] private Vector2? wanderDestination;

        private bool TryReadTarget(out AABB target, out GameObject targetObject)
        {
            Vector2 point;
            target = default;
            targetObject = null;
            switch (type)
            {
                case Behaviour.Trace:
                case Behaviour.Retreat:
                    if (tracing == null || !tracing.HasValue) return false;
                    targetObject = tracing.GameObjectValue;
                    if (!targetObject) return false;
                    Collider2D[] colliders = NavigationBodyGeometry.GetTargetColliders(targetObject);
                    Vector2 fallback = targetObject.transform.position;
                    target = colliders.Length > 0 ? NavigationBodyGeometry.GetMergedAabb(colliders)
                        : AABB.Point(fallback);
                    return true;
                case Behaviour.Wander:
                    wanderDestination ??= GetWanderLocation(GetWanderCenter());
                    point = wanderDestination.Value;
                    break;
                case Behaviour.FixedDestination: point = destination.Vector2Value; break;
                default: point = NavigationGroundAnchor; break;
            }
            target = AABB.Point(point);
            return true;
        }

        private Vector2 GetWanderCenter() => wanderMode switch
        {
            WanderMode.SelfCentered => NavigationGroundAnchor,
            WanderMode.AbsoluteCentered when centerSpace == Space.World => centerOfWander.Vector2Value,
            WanderMode.AbsoluteCentered when centerSpace == Space.Self => centerOfWander.Vector2Value + NavigationGroundAnchor,
            _ => Vector2.zero,
        };

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
        protected abstract Vector2 GetWanderLocation(Vector2 center);

        protected bool IsValidNavigationWanderLocation(Vector2 target, bool requireSupport)
        {
            INavigationWorld world = NavigationWorld;
            Vector2 targetFeet = target;
            if (!IsNavigationDestinationAllowed(NavigationGroundAnchor, targetFeet)) return false;

            Vector2 bodySize = NavigationBodySize;
            AABB candidateBody = AABB.FromMinAndSize(
                new Vector2(targetFeet.x - bodySize.x * 0.5f, targetFeet.y), bodySize);
            if (!world.IsBodyClear(candidateBody, 0f)) return false;
            if (!requireSupport) return true;

            return world.TryResolveSupport(
                targetFeet,
                bodySize,
                NavigationWorldQueries.SupportSnapDistance,
                out _);
        }
    }
}
