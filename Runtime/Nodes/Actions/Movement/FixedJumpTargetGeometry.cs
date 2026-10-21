using Aethiumian.AI.Navigation;
using Aethiumian.AI.Variables;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Owns target measurement and goal conversion shared by fixed-jump execution and feasibility queries.</summary>
    public static class FixedJumpTargetGeometry
    {
        /// <summary>
        /// Captures finite target geometry. Direct object targets require colliders and use their lower center.
        /// PlannedStep may measure transform position and uses it when an object has no colliders.
        /// </summary>
        public static bool TryResolve(VariableField target, FixedJump.JumpTargetMode mode, FixedJump.TargetMeasurement measurement, Vector2 offset, out AABB bounds, out Vector2 landing)
        {
            bounds = default;
            landing = default;
            if (target == null || target.IsNull) return false;
            if (target.IsVector)
            {
                landing = target.Vector2Value + offset;
                bounds = AABB.Point(landing);
            }
            else
            {
                if (!target.IsFromGameObject) return false;
                bool planned = mode == FixedJump.JumpTargetMode.PlannedStep;
                Collider2D[] colliders = planned && measurement == FixedJump.TargetMeasurement.TransformPosition
                    ? null : NavigationBodyGeometry.GetTargetColliders(target.GameObjectValue);
                if (colliders == null || colliders.Length == 0)
                {
                    if (!planned) return false;
                    landing = (Vector2)target.PositionValue + offset;
                    bounds = AABB.Point(landing);
                }
                else
                {
                    bounds = NavigationBodyGeometry.GetMergedAabb(colliders).Translate(offset);
                    landing = bounds.LowerCenter;
                }
            }
            return NavigationNumeric.IsFinite(bounds.Min) && NavigationNumeric.IsFinite(bounds.Max);
        }

        /// <summary>Applies the same distance and line-of-sight goal semantics to execution and preflight.</summary>
        public static NavigationGoalRequest CreateCompletionRequest(AABB target, MovementGoal goal, DistanceMetric metric, float tolerance)
        {
            bool requiresLineOfSight = goal == MovementGoal.Confront || goal == MovementGoal.FiringPosition;
            return goal == MovementGoal.Confront
                ? NavigationGoalRequest.GroundRange(target, tolerance, true)
                : NavigationGoalRequest.Proximity(target, metric, tolerance, requiresLineOfSight);
        }
    }
}
