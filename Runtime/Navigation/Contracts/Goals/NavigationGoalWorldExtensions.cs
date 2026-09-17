using System;
using UnityEngine;
using static Aethiumian.AI.Navigation.Validate;

namespace Aethiumian.AI.Navigation
{
    /// <summary>
    /// Owns the goal-completion decisions that need world facts. Pure target geometry stays on
    /// <see cref="NavigationGoalRequest"/>; this type only adds the optional line-of-sight and
    /// swept-sampling steps on top of it, and performs no environment query when the goal does
    /// not require line of sight.
    /// </summary>
    public static class NavigationGoalWorldExtensions
    {
        /// <summary>
        /// Conservatively rejects only goals no body AABB can touch from inside the finite world.
        /// </summary>
        public static bool CanBodyPossiblyReachGoal(this INavigationWorld world, Vector2 bodySize, NavigationGoalRequest goal)
        {
            if (world == null) return true;
            if (goal.IsRetreat) return true;
            Bounds reachable = goal.IsGroundWalk
                ? goal.GetLowerCenterAcceptanceBounds(bodySize.x)
                : goal.TargetBounds;
            float expansion = Mathf.Max(bodySize.x, bodySize.y) + goal.ArrivalTolerance;
            reachable.Expand(expansion * 2f);
            Bounds worldBounds = new(world.WorldBounds.center, world.WorldBounds.size);
            return reachable.Intersects(worldBounds);
        }


        /// <summary>
        /// Gets the completion distance for a center-anchored body, including the optional
        /// line-of-sight constraint.
        /// </summary>
        public static float GetGoalCompletionDistance(this INavigationWorld world, in NavigationGoalRequest goal, Vector2 center, Vector2 bodySize)
        {
            NonNegativeVector(bodySize, nameof(bodySize));
            if (goal.RequiresLineOfSight && !world.IsLineOfSightClear(center, goal.TargetBounds.Center)) return float.PositiveInfinity;
            return goal.GeometryCompletionDistance(center, bodySize);
        }

        /// <summary>
        /// Returns whether a center-anchored body has completed this goal.
        /// </summary>
        public static bool IsGoalComplete(this INavigationWorld world, in NavigationGoalRequest goal, Vector2 center, Vector2 bodySize)
            => world.GetGoalCompletionDistance(goal, center, bodySize) <= goal.CompletionTolerance;

        /// <summary>
        /// Returns whether one swept center segment enters this goal's finite completion contract.
        /// Without a line-of-sight requirement this is the swept geometry alone, so a fast body that
        /// crosses the goal between two ticks is still recognised without any extra world query.
        /// </summary>
        public static bool IsGoalCompleteAlong(this INavigationWorld world, in NavigationGoalRequest goal, Vector2 startCenter, Vector2 endCenter, Vector2 bodySize)
        {
            if (!goal.RequiresLineOfSight)
                return goal.GeometrySweptCompletionDistance(startCenter, endCenter, bodySize) <= goal.CompletionTolerance;

            Finite(startCenter, nameof(startCenter));
            Finite(endCenter, nameof(endCenter));
            NonNegativeVector(bodySize, nameof(bodySize));
            if (!goal.TryGetGeometryCompletionInterval(startCenter, endCenter, bodySize, out float entry, out float exit))
                return false;

            float spacing = Mathf.Max(NavigationWorldQueries.GeometryEpsilon, NavigationConstant.LineOfSightSweepSpacing);
            double deltaX = (double)endCenter.x - startCenter.x;
            double deltaY = (double)endCenter.y - startCenter.y;
            double intervalLength = Math.Sqrt(deltaX * deltaX + deltaY * deltaY) * (exit - entry);
            double requiredSamples = Math.Ceiling(intervalLength / spacing);
            if (requiredSamples > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(endCenter), "Swept completion interval is too long to sample at the required NavWorld spacing.");
            int samples = Math.Max(1, (int)requiredSamples);
            for (int index = 0; index <= samples; index++)
            {
                float parameter = Mathf.Lerp(entry, exit, index / (float)samples);
                Vector2 sample = Vector2.Lerp(startCenter, endCenter, parameter);
                if (world.IsLineOfSightClear(sample, goal.TargetBounds.Center)) return true;
            }
            return false;
        }
    }
}
