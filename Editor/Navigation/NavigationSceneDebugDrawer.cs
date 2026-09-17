using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AIComponent = Aethiumian.AI.AI;
using MovementNode = Aethiumian.AI.Nodes.Movement;

namespace Aethiumian.AI.Navigation.Editor
{
    /// <summary>Automatically draws live navigation state for the selected AI.</summary>
    [InitializeOnLoad]
    internal static class NavigationSceneDebugDrawer
    {
        private const float SupportDeltaThreshold = 0.05f;
        private const int TrajectorySamples = 16;

        static NavigationSceneDebugDrawer()
        {
            SceneView.duringSceneGui += DrawScene;
            Selection.selectionChanged += RepaintScene;
            AssemblyReloadEvents.beforeAssemblyReload += Unregister;
            EditorApplication.quitting += Unregister;
        }

        /// <summary>Unregisters callbacks before the owning editor assembly is unloaded.</summary>
        private static void Unregister()
        {
            SceneView.duringSceneGui -= DrawScene;
            Selection.selectionChanged -= RepaintScene;
            AssemblyReloadEvents.beforeAssemblyReload -= Unregister;
            EditorApplication.quitting -= Unregister;
        }

        /// <summary>Draws the currently selected AI's live Movement state.</summary>
        private static void DrawScene(SceneView sceneView)
        {
            AIComponent ai = ResolveSelectedAI(Selection.activeGameObject);
            MovementNode movement = GetExecutingMovement(ai);
            if (movement == null) return;

            AABB bodyBounds = movement.NavigationBodyAabb;
            Vector2 groundAnchor = movement.NavigationGroundAnchor;
            Handles.color = Color.white;
            DrawBounds(bodyBounds);
            Handles.DrawSolidDisc(groundAnchor, Vector3.forward, 0.035f);
            Rigidbody2D body = movement.RigidBody;
            if (body) DrawLine(1f, groundAnchor, groundAnchor + body.linearVelocity * 0.15f);

            GroundTraversalExecutor executor = movement.Executor as GroundTraversalExecutor;
            INavigationWorld world = TryGetWorld(movement.NavigationRuntime);
            DrawRoute(movement.Route, movement.ActiveSegment, executor, world,
                new Color(0.35f, 0.35f, 0.35f));
            DrawSupport(movement, bodyBounds, groundAnchor, world);
            DrawExecutor(executor, groundAnchor);
            DrawStatus(ai, movement, executor, movement.Route, movement.RouteIndex, bodyBounds.Center);
        }

        /// <summary>Draws one live route directly from its immutable segments.</summary>
        private static void DrawRoute(NavigationRoute route, NavigationRouteSegment committed, GroundTraversalExecutor executor, INavigationWorld world, Color fallback)
        {
            if (route == null) return;
            for (int index = 0; index < route.Segments.Count; index++)
            {
                NavigationRouteSegment segment = route.Segments[index];
                DrawSegment(segment, index, ReferenceEquals(segment, committed), executor, world, fallback);
            }
        }

        /// <summary>Draws one segment using its existing planned or executing geometry.</summary>
        private static void DrawSegment(NavigationRouteSegment segment, int index, bool committed, GroundTraversalExecutor executor, INavigationWorld world, Color fallback)
        {
            Handles.color = GetSegmentColor(segment, fallback);
            float width = committed ? 4f : 2f;
            if (segment is JumpRouteSegment jump)
            {
                JumpTrajectorySolution trajectory = executor != null
                    && executor.CurrentAction == GroundTraversalExecutor.ActionKind.Jump
                    && executor.CurrentJumpTrajectory != null
                    && executor.CurrentJumpTrajectory.StartPosition == jump.Start
                    && executor.CurrentJumpTrajectory.LandingPosition == jump.End
                        ? executor.CurrentJumpTrajectory
                        : null;
                if (trajectory != null) DrawTrajectory(trajectory, width);
                else DrawPolyline(width, jump.Start,
                    jump.Start + Vector2.up * jump.MinimumApexHeight, jump.End);
                DrawCrossings(jump.SurfaceCrossings, world);
            }
            else if (segment is FallRouteSegment fall)
                DrawPolyline(width, fall.Start, fall.LedgeExit, fall.End);
            else if (segment is DropThroughRouteSegment)
                DrawArrow(segment.Start, segment.End, width);
            else
                DrawLine(width, segment.Start, segment.End);

            Handles.Label(segment.Start, $"{index}: {segment.GetType().Name}");
        }

        /// <summary>Draws the executor's current action and collision lease count.</summary>
        private static void DrawExecutor(GroundTraversalExecutor executor, Vector2 groundAnchor)
        {
            if (executor == null || executor.CurrentAction == GroundTraversalExecutor.ActionKind.None) return;
            Handles.color = GetActionColor(executor.CurrentAction);
            if (executor.CurrentAction == GroundTraversalExecutor.ActionKind.Jump
                && executor.CurrentJumpTrajectory != null)
                DrawTrajectory(executor.CurrentJumpTrajectory, 3f);
            else if (executor.CurrentAction == GroundTraversalExecutor.ActionKind.Fall)
                DrawPolyline(3f, executor.CurrentActionStart, executor.CurrentLedgeExit, executor.CurrentActionEnd);
            else if (executor.CurrentAction == GroundTraversalExecutor.ActionKind.DropThrough)
                DrawArrow(executor.CurrentActionStart, executor.CurrentActionEnd, 3f);
            else
                DrawLine(3f, executor.CurrentActionStart, executor.CurrentActionEnd);

            Handles.Label(groundAnchor,
                $"{executor.CurrentAction} / {executor.CurrentActionElapsedSeconds:0.00}s\n" +
                $"Collision leases {executor.PlatformCollisionLeaseCount}");
        }

        /// <summary>Queries and draws current physics and NavWorld support without retaining either result.</summary>
        private static void DrawSupport(MovementNode movement, AABB bodyBounds, Vector2 groundAnchor, INavigationWorld world)
        {
            MapNavigationRuntime runtime = movement.NavigationRuntime;
            bool hasPhysicsSupport = TryGetPhysicalSupport(runtime, movement.Collider, out Vector2 physicsSupport);
            Vector2 navigationSupport = default;
            NavigationSupport support = default;
            bool hasNavigationSupport = runtime != null
                && !runtime.IsDisposed
                && runtime.TryResolvePlanningGroundSupport(groundAnchor, bodyBounds.Size, out navigationSupport, out support);

            if (hasPhysicsSupport)
            {
                Handles.color = Color.white;
                Handles.DrawSolidDisc(physicsSupport, Vector3.forward, 0.04f);
            }
            if (hasNavigationSupport)
            {
                Handles.color = Color.white;
                Handles.DrawSolidDisc(navigationSupport, Vector3.forward, 0.03f);
                Handles.Label(navigationSupport,
                    $"{support.Kind} {support.Surface.SourceId}:{support.Surface.FeatureId}");
            }
            if (hasPhysicsSupport && hasNavigationSupport
                && Vector2.Distance(physicsSupport, navigationSupport) > SupportDeltaThreshold)
            {
                Handles.color = Color.red;
                Handles.DrawDottedLine(physicsSupport, navigationSupport, 3f);
                Handles.Label((physicsSupport + navigationSupport) * 0.5f,
                    $"support delta {Vector2.Distance(physicsSupport, navigationSupport):0.00}");
            }
        }

        /// <summary>Draws current navigation status beside the selected body.</summary>
        private static void DrawStatus(AIComponent ai, MovementNode movement, GroundTraversalExecutor executor,
            NavigationRoute route, int routeIndex, Vector2 labelPosition)
        {
            Handles.color = Color.white;
            int revision = movement.NavigationRuntime?.SnapshotRevision ?? 0;
            string routeStatus = route == null
                ? "Route None"
                : $"Route {routeIndex}/{route.Count} / reaches-goal {route.ReachesGoal}";
            Handles.Label(labelPosition,
                $"{ai.name} / {movement.GetType().Name}\n" +
                $"Mode {movement.path} / {movement.type} / snapshot {revision}\n" +
                routeStatus + "\n" +
                $"Executor {(executor == null ? "None" : executor.CurrentAction.ToString())} / " +
                $"{(executor == null ? 0f : executor.CurrentActionElapsedSeconds):0.00}s");
        }

        /// <summary>Gets the current published NavWorld, if available.</summary>
        private static INavigationWorld TryGetWorld(MapNavigationRuntime map)
            => map != null && map.TryGetWorld(out INavigationWorld world) ? world : null;

        /// <summary>
        /// Reads the nearest current physics support for the displayed anchor.
        /// </summary>
        private static bool TryGetPhysicalSupport(MapNavigationRuntime runtime, Collider2D bodyCollider, out Vector2 supportPoint)
        {
            supportPoint = default;

            if (!bodyCollider || runtime == null || runtime.IsDisposed)
                return false;

            return NavigationWorldQueries.TryGetGroundSupportPoint(
                bodyCollider,
                runtime.CreateTerrainFilter(),
                out supportPoint);
        }

        /// <summary>Draws a solved trajectory without running the solver.</summary>
        private static void DrawTrajectory(JumpTrajectorySolution trajectory, float width)
        {
            Vector3[] points = new Vector3[TrajectorySamples + 1];
            for (int index = 0; index <= TrajectorySamples; index++)
                points[index] = trajectory.GetPosition(trajectory.FlightDuration * index / TrajectorySamples);
            Handles.DrawAAPolyLine(width, points);
        }

        /// <summary>Draws route-owned platform crossings using captured surface provenance.</summary>
        private static void DrawCrossings(IReadOnlyList<JumpSurfaceCrossing> crossings, INavigationWorld world)
        {
            if (crossings == null || world == null) return;
            for (int index = 0; index < crossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = crossings[index];
                Handles.Label(crossing.Position,
                    $"{crossing.Kind} {crossing.Surface.SourceId}:{crossing.Surface.FeatureId} " +
                    $"y={crossing.Position.y:0.00}");
            }
        }

        /// <summary>Draws a rectangular world-space bounds outline.</summary>
        private static void DrawBounds(AABB bounds)
        {
            Vector3 min = bounds.Min;
            Vector3 max = bounds.Max;
            Handles.DrawLine(new Vector3(min.x, min.y), new Vector3(max.x, min.y));
            Handles.DrawLine(new Vector3(max.x, min.y), new Vector3(max.x, max.y));
            Handles.DrawLine(new Vector3(max.x, max.y), new Vector3(min.x, max.y));
            Handles.DrawLine(new Vector3(min.x, max.y), new Vector3(min.x, min.y));
        }

        /// <summary>Draws a directional segment used for DropThrough actions.</summary>
        private static void DrawArrow(Vector2 start, Vector2 end, float width)
        {
            DrawLine(width, start, end);
            Vector2 direction = (end - start).normalized;
            if (direction.sqrMagnitude <= 0.000001f) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            Handles.ConeHandleCap(0, end, Quaternion.Euler(0f, 0f, angle), 0.12f, EventType.Repaint);
        }

        /// <summary>Draws a polyline with a stable Scene view width.</summary>
        private static void DrawPolyline(float width, params Vector2[] points)
        {
            Vector3[] converted = new Vector3[points.Length];
            for (int index = 0; index < points.Length; index++) converted[index] = points[index];
            Handles.DrawAAPolyLine(width, converted);
        }

        /// <summary>Draws a line with a stable Scene view width.</summary>
        private static void DrawLine(float width, Vector2 start, Vector2 end) => Handles.DrawAAPolyLine(width, (Vector3)start, (Vector3)end);

        /// <summary>Gets the fixed route color for a segment type.</summary>
        private static Color GetSegmentColor(NavigationRouteSegment segment, Color fallback)
            => segment is JumpRouteSegment ? Color.yellow
                : segment is FallRouteSegment ? Color.blue
                : segment is DropThroughRouteSegment ? new Color(0.75f, 0f, 0.75f)
                : segment is GroundRouteSegment ? Color.green
                : segment is FlyRouteSegment ? Color.white
                : fallback;

        /// <summary>Gets the fixed color for an executor action.</summary>
        private static Color GetActionColor(GroundTraversalExecutor.ActionKind action)
            => action == GroundTraversalExecutor.ActionKind.Jump ? Color.yellow
                : action == GroundTraversalExecutor.ActionKind.Fall ? Color.blue
                : action == GroundTraversalExecutor.ActionKind.DropThrough ? new Color(0.75f, 0f, 0.75f)
                : Color.green;

        /// <summary>Resolves a selected AI root or child object.</summary>
        private static AIComponent ResolveSelectedAI(GameObject selected)
        {
            if (!selected) return null;
            AIComponent ai = selected.GetComponent<AIComponent>();
            return ai ? ai : selected.GetComponentInParent<AIComponent>();
        }

        /// <summary>Gets the currently executing Movement node without reflection.</summary>
        private static MovementNode GetExecutingMovement(AIComponent ai)
            => ai && ai.BehaviourTree != null ? ai.BehaviourTree.ExecutingNode as MovementNode : null;

        /// <summary>Repaints Scene views after the editor selection changes.</summary>
        private static void RepaintScene() => SceneView.RepaintAll();
    }
}
