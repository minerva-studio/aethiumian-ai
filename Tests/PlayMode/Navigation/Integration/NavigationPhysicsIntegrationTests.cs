using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Verifies the real fixed-step physics boundary shared by navigation planning and execution.
    /// </summary>
    public sealed class NavigationPhysicsIntegrationTests
    {
        private const float GravityScale = 4f;
        private const float LinearDamping = 5f;
        private const float PositionTolerance = 0.01f;
        private static readonly Vector2 PhysicsOrigin = new(-1000f, -1000f);
        private readonly List<GameObject> objects = new();
        private bool geometryCollisionWasIgnored;

        [SetUp]
        public void SetUp()
        {
            geometryCollisionWasIgnored = Physics2D.GetIgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer);
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                false);
        }

        /// <summary>Destroys every isolated physics object after each test.</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics2D.IgnoreLayerCollision(
                NavigationPhysicsTestLayers.DefaultLayer,
                NavigationPhysicsTestLayers.GeometryLayer,
                geometryCollisionWasIgnored);
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index]) UnityEngine.Object.Destroy(objects[index]);
            }

            objects.Clear();
            yield return null;
        }

        /// <summary>Verifies a Walk plan containing a jump executes through the same real body and collider.</summary>
        [UnityTest]
        public IEnumerator WalkPlanner_JumpPlanExecutesToResolvedGoal()
        {
            PhysicsAlignedNavigationWorld world = CreateObstacleWorld();
            CreateBox("navigation-floor", PhysicsOrigin + new Vector2(2.5f, 0.5f), new Vector2(5f, 1f), NavigationPhysicsTestLayers.GeometryLayer);
            CreateBox("navigation-obstacle", PhysicsOrigin + new Vector2(2.5f, 1.5f), Vector2.one, NavigationPhysicsTestLayers.GeometryLayer);
            (Rigidbody2D body, BoxCollider2D collider) = CreateBody(PhysicsOrigin + new Vector2(0.5f, 3f), addCollider: true);
            collider.size = new Vector2(0.8f, 1.5f);
            body.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionX;
            Physics2D.SyncTransforms();

            float startSurfaceY = PhysicsOrigin.y + 1f;
            for (int tick = 0; tick < 120 && !IsSettledOnSupport(collider, startSurfaceY); tick++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(IsSettledOnSupport(collider, startSurfaceY), Is.True,
                $"The integration body did not settle onto support below its feet; position={body.position}.");
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.linearVelocity = Vector2.zero;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Vector2 observedStart = NavigationBodyGeometry.GetGroundAnchor(collider);
            WalkNavigationParameters parameters = new(collider.bounds.size, 5f, Physics2D.gravity, GravityScale, LinearDamping, 2.5f, 5f, Time.fixedDeltaTime);
            var planner = new WalkNavigationPlanner(world, 256, new GroundJumpSolver(world));
            var diagnostics = new NavigationPlanningDiagnostics();
            AABB targetBounds = new(PhysicsOrigin + new Vector2(4.5f, 1f), PhysicsOrigin + new Vector2(4.5f, 1f));
            NavigationGoalRequest goal = NavigationGoalRequest.Proximity(targetBounds, DistanceMetric.Euclidean, 0.1f);

            NavigationRoute route = planner.Plan(observedStart, goal, parameters, CancellationToken.None, diagnostics).Route;
            Assert.That(route, Is.Not.Null,
                $"Walk planning failed from observed contact anchor {observedStart}; "
                + $"expansions={diagnostics.ExpansionCount}, terminals={diagnostics.TerminalCandidateCount}, "
                + $"generatedJumps={diagnostics.JumpCandidateGeneratedCount}, validatedJumps={diagnostics.JumpCandidateValidatedCount}.");
            Assert.That(ContainsSegment<JumpRouteSegment>(route), Is.True, "The physical obstacle must require a planned JumpRouteSegment.");

            int jumpCallbacks = 0;
            bool leftGround = false;
            float settledAnchorY = observedStart.y;
            using var executor = new GroundTraversalExecutor(body, collider, CreateTerrainFilter(), parameters.Speed, 1f, onJump: () => jumpCallbacks++);
            for (int stepIndex = 0; stepIndex < route.Count; stepIndex++)
            {
                NavigationRouteSegment segment = route.Segments[stepIndex];
                switch (segment)
                {
                    case GroundRouteSegment ground:
                        executor.SetGroundMove(ground.Start, ground.End);
                        break;
                    case JumpRouteSegment jump:
                        Assert.That(JumpTrajectory.TrySolve(new JumpTrajectoryInput(
                            NavigationBodyGeometry.GetGroundAnchor(collider), jump.PlannedLanding,
                            parameters.Gravity, parameters.GravityScale, parameters.LinearDamping,
                            parameters.JumpHeight, parameters.SimulationTimeStep), 512,
                            jump.MinimumApexHeight,
                            out JumpTrajectorySolution trajectory), Is.True);
                        executor.BeginJump(trajectory);
                        break;
                    case FallRouteSegment fall:
                        executor.BeginFall(fall.Start, fall.LedgeExit, fall.End);
                        break;
                    case DropThroughRouteSegment dropThrough:
                        executor.BeginDropThrough(dropThrough.Start, dropThrough.End);
                        break;
                    default:
                        Assert.Fail($"Unsupported route segment {segment.GetType().Name}.");
                        break;
                }
                ExecutionStatus completed = ExecutionStatus.Running;
                for (int tick = 0; tick < 360; tick++)
                {
                    completed = executor.Tick(Time.fixedDeltaTime).Status;
                    if (completed != ExecutionStatus.Running) break;
                    yield return new WaitForFixedUpdate();
                    leftGround |= NavigationBodyGeometry.GetGroundAnchor(collider).y > settledAnchorY + 0.02f;
                }

                Assert.That(completed, Is.EqualTo(ExecutionStatus.Completed),
                    $"Route segment {stepIndex} ({segment.GetType().Name}) did not complete; "
                    + $"position=({body.position.x:F6}, {body.position.y:F6}), "
                    + $"velocity=({body.linearVelocity.x:F6}, {body.linearVelocity.y:F6}), "
                    + $"anchor=({NavigationBodyGeometry.GetGroundAnchor(collider).x:F6}, "
                    + $"{NavigationBodyGeometry.GetGroundAnchor(collider).y:F6}), "
                    + $"supportSurfaceY={GetSupportSurfaceY(collider):F6}, "
                    + $"contactOffset={Physics2D.defaultContactOffset:F6}, "
                    + $"grounded={body.IsTouchingLayers(NavigationPhysicsTestLayers.TerrainMask)}, segment={segment.Start}->{segment.End}.");
            }

            Vector2 finalAnchor = NavigationBodyGeometry.GetGroundAnchor(collider);
            Assert.That(jumpCallbacks, Is.GreaterThanOrEqualTo(1));
            Assert.That(leftGround, Is.True);
            Assert.That(Mathf.Abs(finalAnchor.x - route.ResolvedGoal.x),
                Is.LessThanOrEqualTo(Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon));
            Assert.That(Mathf.Abs(GetSupportSurfaceY(collider) - route.ResolvedGoal.y),
                Is.LessThanOrEqualTo(Physics2D.defaultContactOffset + NavigationWorldQueries.GeometryEpsilon));
        }

        /// <summary>Creates the physical body profile used by real damped navigation tests.</summary>
        private (Rigidbody2D body, BoxCollider2D collider) CreateBody(Vector2 position, bool addCollider)
        {
            GameObject host = new("navigation-physics-body");
            objects.Add(host);
            host.transform.position = position;
            Rigidbody2D body = host.AddComponent<Rigidbody2D>();
            body.gravityScale = GravityScale;
            body.linearDamping = LinearDamping;
            body.freezeRotation = true;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            BoxCollider2D collider = addCollider ? host.AddComponent<BoxCollider2D>() : null;
            return (body, collider);
        }

        /// <summary>Creates one physics box whose geometry exactly matches the test navigation cells.</summary>
        private BoxCollider2D CreateBox(string name, Vector2 position, Vector2 size, int layer)
        {
            GameObject host = new(name) { layer = layer };
            objects.Add(host);
            host.transform.position = position;
            BoxCollider2D collider = host.AddComponent<BoxCollider2D>();
            collider.size = size;
            return collider;
        }

        /// <summary>Creates the package-local terrain configuration without a scene facade.</summary>
        private static ContactFilter2D CreateTerrainFilter()
            => NavigationPhysicsTestLayers.CreateTerrainFilter();

        /// <summary>Gets the nearest physical support surface below a landed test body.</summary>
        private static float GetSupportSurfaceY(Collider2D collider)
        {
            bool found = TryGetSupportSurfaceY(collider, out float surfaceY);
            Assert.That(found, Is.True, "No physical support surface was found below the landed body.");
            return surfaceY;
        }

        /// <summary>Checks that the body has resolved penetration and rests at the physics contact gap.</summary>
        private static bool IsSettledOnSupport(Collider2D collider, float expectedSurfaceY)
        {
            float anchorGap = NavigationBodyGeometry.GetGroundAnchor(collider).y - expectedSurfaceY;
            return anchorGap >= -NavigationWorldQueries.GeometryEpsilon
                && anchorGap <= NavigationWorldQueries.SupportSnapDistance + NavigationWorldQueries.GeometryEpsilon;
        }

        /// <summary>Finds the nearest physical support surface directly below a test body.</summary>
        private static bool TryGetSupportSurfaceY(Collider2D collider, out float surfaceY)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = NavigationPhysicsTestLayers.TerrainMask,
                useTriggers = false,
            };
            RaycastHit2D[] hits = new RaycastHit2D[8];
            int count = collider.Cast(Vector2.down, filter, hits, 0.08f);
            float nearestDistance = float.PositiveInfinity;
            surfaceY = float.NaN;
            for (int index = 0; index < count; index++)
            {
                RaycastHit2D hit = hits[index];
                if (!hit.collider || hit.collider == collider || hit.distance >= nearestDistance) continue;
                nearestDistance = hit.distance;
                surfaceY = hit.point.y;
            }

            return !float.IsNaN(surfaceY);
        }

        /// <summary>Creates the immutable grid represented by the floor and obstacle physics boxes.</summary>
        private static PhysicsAlignedNavigationWorld CreateObstacleWorld()
        {
            List<Vector2Int> solids = new();
            for (int x = 0; x < 5; x++) solids.Add(new Vector2Int(x, 0));
            solids.Add(new Vector2Int(2, 1));
            return new PhysicsAlignedNavigationWorld(PhysicsOrigin, new AABBInt(0, 0, 5, 7), solids);
        }

        /// <summary>Returns whether a plan contains a traversal step of the requested type.</summary>
        private static bool ContainsSegment<TSegment>(NavigationRoute route) where TSegment : NavigationRouteSegment
        {
            for (int index = 0; index < route.Count; index++)
            {
                if (route.Segments[index] is TSegment) return true;
            }

            return false;
        }

        /// <summary>Asserts component-wise vector equality with a diagnostic stage label.</summary>
        private static void AssertVector(Vector2 actual, Vector2 expected, float tolerance, string stage)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance), $"Unexpected x component for {stage}.");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance), $"Unexpected y component for {stage}.");
        }

        /// <summary>Small immutable world whose solid cells are mirrored by real physics colliders.</summary>
        private sealed class PhysicsAlignedNavigationWorld : INavigationWorld
        {
            private readonly NavigationWorldSnapshot snapshot;

            /// <summary>Gets the captured finite world rectangle.</summary>
            public AABB WorldBounds => snapshot.WorldBounds;

            /// <summary>Creates an immutable test world from exact solid cell coordinates.</summary>
            public PhysicsAlignedNavigationWorld(Vector2 origin, AABBInt cellBounds, IEnumerable<Vector2Int> solidCells)
            {
                if (solidCells == null) throw new ArgumentNullException(nameof(solidCells));
                List<NavigationShapeData> shapes = new();
                int sourceId = 0;
                foreach (Vector2Int cell in solidCells)
                {
                    Vector2 min = origin + Vector2.Scale(cell, Vector2.one);
                    Vector2 max = min + Vector2.one;
                    shapes.Add(new NavigationShapeData(sourceId++, 0, NavigationShapeType.Polygon,
                        new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) },
                        0f, NavigationSurfaceKind.Solid, true));
                }
                snapshot = NavigationWorldSnapshot.Create(AABB.FromMinAndSize(
                    new Vector2(origin.x + cellBounds.MinX, origin.y + cellBounds.MinY),
                    new Vector2(cellBounds.SizeX, cellBounds.SizeY)), shapes,
                    Array.Empty<NavigationRegionData>());
            }

            public bool IsBodyClear(AABB bodyBounds, float tolerance) => snapshot.IsBodyClear(bodyBounds, tolerance);
            public bool IsBodyPathClear(AABB bodyBounds, Vector2 displacement, float tolerance)
                => snapshot.IsBodyPathClear(bodyBounds, displacement, tolerance);
            public bool IsLineOfSightClear(Vector2 start, Vector2 end) => snapshot.IsLineOfSightClear(start, end);
            public bool TryGetSupportBelow(Vector2 position, out NavigationSupport support)
                => snapshot.TryGetSupportBelow(position, out support);
            public bool TryResolveSupport(Vector2 feet, Vector2 bodySize, float snapDistance, out NavigationSupport support)
                => snapshot.TryResolveSupport(feet, bodySize, snapDistance, out support);
            public IReadOnlyList<NavigationSupportCandidate> GetSupportCandidates(AABB anchorBounds, Vector2 bodySize)
                => snapshot.GetSupportCandidates(anchorBounds, bodySize);
            public void CollectSupportCandidates(AABB anchorBounds, Vector2 bodySize, List<NavigationSupportCandidate> results)
                => snapshot.CollectSupportCandidates(anchorBounds, bodySize, results);
            public void CollectOneWayCrossings(Vector2 previousFeet, Vector2 currentFeet, float bodyWidth,
                List<NavigationSurfaceCrossing> results)
                => snapshot.CollectOneWayCrossings(previousFeet, currentFeet, bodyWidth, results);
            public bool AreInSameRegion(Vector2 first, Vector2 second) => snapshot.AreInSameRegion(first, second);
        }
    }
}
