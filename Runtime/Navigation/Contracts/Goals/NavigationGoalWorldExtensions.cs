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
            AABB reachable = goal.IsGroundWalk
                ? goal.GetLowerCenterAcceptanceBounds(bodySize.x)
                : goal.TargetBounds;
            float expansion = Mathf.Max(bodySize.x, bodySize.y) + goal.ArrivalTolerance;
            return reachable.Expand(expansion).Intersects(world.WorldBounds);
        }


        /// <summary>
        /// Gets the completion distance for a body, including the optional line-of-sight constraint.
        /// </summary>
        public static float GetGoalCompletionDistance(this INavigationWorld world, in NavigationGoalRequest goal, AABB body)
        {
            Aabb(body, nameof(body));
            if (goal.RequiresLineOfSight && !world.IsLineOfSightClear(body.Center, goal.TargetBounds.Center)) return float.PositiveInfinity;
            return goal.GeometryCompletionDistance(body);
        }

        /// <summary>
        /// Returns whether a body has completed this goal.
        /// </summary>
        public static bool IsGoalComplete(this INavigationWorld world, in NavigationGoalRequest goal, AABB body)
            => world.GetGoalCompletionDistance(goal, body) <= goal.CompletionTolerance;

        /// <summary>
        /// Returns whether one swept body enters this goal's finite completion contract. Without a
        /// line-of-sight requirement this is the swept geometry alone, so a fast body that crosses the
        /// goal between two ticks is still recognised without any extra world query.
        /// </summary>
        public static bool IsGoalCompleteAlong(this INavigationWorld world, in NavigationGoalRequest goal, AABB startBody, AABB endBody)
        {
            if (!goal.RequiresLineOfSight)
                return goal.GeometrySweptCompletionDistance(startBody, endBody) <= goal.CompletionTolerance;

            Aabb(startBody, nameof(startBody));
            Aabb(endBody, nameof(endBody));
            if (!goal.TryGetGeometryCompletionInterval(startBody, endBody, out float entry, out float exit))
                return false;

            Vector2 startCenter = startBody.Center;
            Vector2 endCenter = endBody.Center;
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
