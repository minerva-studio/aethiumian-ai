using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns one Map's immutable published world and finite planning queue.</summary>
    public sealed class MapNavigationRuntime : IDisposable
    {
        private readonly NavigationPlanningScheduler scheduler;
        private readonly int groundExpansionLimit;
        private readonly int flyExpansionLimit;
        private GroundJumpSolver jumpSolver;
        private WalkNavigationPlanner walkPlanner;
        private JumpNavigationPlanner jumpPlanner;
        private FlyNavigationPlanner flyPlanner;
        private readonly List<PendingRequest> pendingRequests = new();
        private INavigationWorld world;
        private Dictionary<int, Collider2D> sourceColliders;
        private Exception worldBuildException;
        private bool isDisposed;

        /// <summary>Gets the immutable terrain configuration shared by capture and execution.</summary>
        public NavigationPhysicsLayers PhysicsLayers { get; }

        /// <summary>Gets whether a NavWorld has been published and this runtime remains available.</summary>
        public bool IsReady => !isDisposed && world != null;

        /// <summary>Gets whether the owning world lifetime has ended.</summary>
        public bool IsDisposed => isDisposed;

        /// <summary>Gets the compatibility snapshot revision for existing editor and coordinator diagnostics.</summary>
        public int SnapshotRevision => !isDisposed && world != null ? 1 : 0;

        /// <summary>Creates a planning runtime with empty execution layers and no project-specific defaults.</summary>
        public MapNavigationRuntime(int queueCapacity, int groundExpansionLimit, int flyExpansionLimit)
            : this(queueCapacity, groundExpansionLimit, flyExpansionLimit, default)
        {
        }

        /// <summary>Creates a world runtime with an explicit capture and execution layer configuration.</summary>
        public MapNavigationRuntime(int queueCapacity, int groundExpansionLimit, int flyExpansionLimit, NavigationPhysicsLayers physicsLayers)
        {
            if (groundExpansionLimit <= 0) throw new ArgumentOutOfRangeException(nameof(groundExpansionLimit));
            if (flyExpansionLimit <= 0) throw new ArgumentOutOfRangeException(nameof(flyExpansionLimit));
            NavigationWorldQueries.CaptureSupportSnapDistance();
            GroundTraversalEndpointPolicy.CaptureVerticalSupportTolerance();
            this.groundExpansionLimit = groundExpansionLimit;
            this.flyExpansionLimit = flyExpansionLimit;
            PhysicsLayers = physicsLayers;
            scheduler = new NavigationPlanningScheduler(queueCapacity);
        }

        /// <summary>Publishes the immutable world used by all planner callbacks in this Map lifetime.</summary>
        public void PublishWorld(INavigationWorld navigationWorld) => PublishWorld(navigationWorld, null);

        /// <summary>Publishes a world and its atomic execution-side source bindings together.</summary>
        public void PublishWorld(INavigationWorld navigationWorld, IReadOnlyDictionary<int, Collider2D> bindings)
        {
            ThrowIfDisposed();
            if (worldBuildException != null) throw new InvalidOperationException("Navigation world construction has already failed.", worldBuildException);
            if (world != null) throw new InvalidOperationException("Navigation world has already been published.");

            INavigationWorld snapshot = navigationWorld ?? throw new ArgumentNullException(nameof(navigationWorld));
            GroundJumpSolver createdJumpSolver = new(snapshot);
            WalkNavigationPlanner createdWalk = new(snapshot, groundExpansionLimit, createdJumpSolver);
            JumpNavigationPlanner createdJump = new(snapshot, groundExpansionLimit, createdJumpSolver);
            FlyNavigationPlanner createdFly = new(snapshot, flyExpansionLimit);
            Dictionary<int, Collider2D> capturedBindings = bindings == null ? null : new Dictionary<int, Collider2D>(bindings);
            world = snapshot;
            jumpSolver = createdJumpSolver;
            walkPlanner = createdWalk;
            jumpPlanner = createdJump;
            flyPlanner = createdFly;
            sourceColliders = capturedBindings;
            for (int i = 0; i < pendingRequests.Count; i++) Schedule(pendingRequests[i]);
            scheduler.Start();
        }

        /// <summary>
        /// Fails all waiting requests and makes later requests fail immediately with the same exception.
        /// </summary>
        public void FailWorld(Exception failure)
        {
            ThrowIfDisposed();
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            if (world != null) throw new InvalidOperationException("Navigation world has already been published.");
            if (worldBuildException != null) return;
            worldBuildException = failure;
            for (int i = 0; i < pendingRequests.Count; i++)
                pendingRequests[i].Operation.TryFail(failure);
            ReleaseCompletedOperations();
        }

        /// <summary>Creates execution candidates even before geometry publication; invalid after disposal.</summary>
        public ContactFilter2D CreateTerrainFilter()
        {
            ThrowIfDisposed();
            return PhysicsLayers.CreateTerrainFilter();
        }

        /// <summary>Returns the currently published immutable world without exposing runtime ownership.</summary>
        public bool TryGetWorld(out INavigationWorld snapshot)
        {
            snapshot = world;
            return !isDisposed && snapshot != null;
        }

        /// <summary>Returns the runtime-owned shared jump solver only after its world has been published.</summary>
        public bool TryGetJumpSolver(out GroundJumpSolver solver)
        {
            solver = jumpSolver;
            return !isDisposed && solver != null;
        }

        /// <summary>
        /// Queues ground planning with an explicit local-action or route horizon.
        /// </summary>
        public NavigationPlanningOperation PlanWalkAsync(AABB body,
            NavigationGoalRequest goalRequest,
            WalkNavigationParameters parameters,
            NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default)
        {
            return QueueWork(new PendingRequest<WalkNavigationPlanner, WalkNavigationParameters>(body, goalRequest, parameters, extent), cancellationToken);
        }

        /// <summary>
        /// Queues jump planning without requiring a complete path for NextAction.
        /// </summary>
        public NavigationPlanningOperation PlanJumpAsync(AABB body,
            NavigationGoalRequest goalRequest,
            JumpNavigationParameters parameters,
            NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default)
        {
            return QueueWork(new PendingRequest<JumpNavigationPlanner, JumpNavigationParameters>(body, goalRequest, parameters, extent), cancellationToken);
        }

        /// <summary>
        /// Queues aerial planning with the same horizon contract as ground movement.
        /// </summary>
        public NavigationPlanningOperation PlanFlyAsync(AABB body,
            NavigationGoalRequest goalRequest,
            FlyNavigationParameters parameters,
            NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default)
        {
            return QueueWork(new PendingRequest<FlyNavigationPlanner, FlyNavigationParameters>(body, goalRequest, parameters, extent), cancellationToken);
        }

        /// <summary>Checks a body-clear aerial sweep against the published immutable world.</summary>
        public bool IsBodyClearFlySegment(AABB startBody, Vector2 displacement)
        {
            ThrowIfDisposed();
            return world != null && world.IsBodyPathClear(startBody, displacement, 0f);
        }

        /// <summary>Cancels pending requests and releases the published world at the Map cleanup boundary.</summary>
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            for (int i = 0; i < pendingRequests.Count; i++)
            {
                pendingRequests[i].Cancellation.Cancel();
                pendingRequests[i].Operation.TryFinalizeCancellation();
                pendingRequests[i].Operation.ReleaseCancellationRegistration();
            }
            scheduler.FailPending(new ObjectDisposedException(nameof(MapNavigationRuntime)));
            scheduler.Dispose();
            for (int i = 0; i < pendingRequests.Count; i++) pendingRequests[i].Cancellation.Dispose();
            pendingRequests.Clear();
            world = null;
            jumpSolver = null;
            walkPlanner = null;
            jumpPlanner = null;
            flyPlanner = null;
            sourceColliders = null;
        }

        /// <summary>Resolves a captured surface identity only against the currently published Map snapshot.</summary>
        public bool TryResolveSource(NavigationSurfaceId surface, out Collider2D collider)
        {
            collider = null;
            if (isDisposed || sourceColliders == null || !sourceColliders.TryGetValue(surface.SourceId, out collider)) return false;
            return collider && collider.isActiveAndEnabled && !collider.isTrigger;
        }

        /// <summary>Queues a typed request and creates detached planner work after world publication.</summary>
        private NavigationPlanningOperation QueueWork(PendingRequest request, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ReleaseCompletedOperations();
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (worldBuildException != null) return NavigationPlanningOperation.CreateFailed(worldBuildException);
            CancellationTokenSource ownedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                NavigationPlanningOperation operation = new();
                operation.RegisterCancellation(ownedCancellation.Token);
                request.Initialize(operation, ownedCancellation);
                pendingRequests.Add(request);
                if (world != null) Schedule(request);
                return operation;
            }
            catch
            {
                ownedCancellation.Dispose();
                throw;
            }
        }

        /// <summary>Constructs and binds each pending request exactly once before it enters the background queue.</summary>
        private void Schedule(PendingRequest request)
        {
            if (request.Operation.IsCompleted) return;
            if (request.Operation.IsCancellationRequested)
            {
                request.Operation.TryFinalizeCancellation();
                return;
            }
            try
            {
                NavigationGoalRequest goal = request.Goal;
                if (!world.CanBodyPossiblyReachGoal(request.BodySize, goal))
                {
                    request.Operation.TryComplete(NavigationPlanResult.NoResult);
                    return;
                }

                INavigationPlanningWork work = request.CreateWork(this);
                scheduler.PlanWork(work, request.Cancellation.Token, request.Operation);
            }
            catch (Exception exception)
            {
                request.Operation.TryFail(exception);
            }
        }

        /// <summary>Releases linked cancellation sources for operations finalized by the scheduler.</summary>
        public void ReleaseCompletedOperations()
        {
            if (isDisposed) return;
            for (int i = pendingRequests.Count - 1; i >= 0; i--)
            {
                PendingRequest request = pendingRequests[i];
                if (!request.Operation.IsCompleted) continue;
                request.Operation.ReleaseCancellationRegistration();
                request.Cancellation.Dispose();
                pendingRequests.RemoveAt(i);
            }
        }

        public TPlanner GetPlanner<TPlanner>() where TPlanner : class
        {
            if (typeof(TPlanner) == typeof(WalkNavigationPlanner)) return walkPlanner as TPlanner ?? throw new InvalidOperationException("Walk planner is not available because the world has not been published.");
            if (typeof(TPlanner) == typeof(JumpNavigationPlanner)) return jumpPlanner as TPlanner ?? throw new InvalidOperationException("Jump planner is not available because the world has not been published.");
            if (typeof(TPlanner) == typeof(FlyNavigationPlanner)) return flyPlanner as TPlanner ?? throw new InvalidOperationException("Fly planner is not available because the world has not been published.");
            throw new NotSupportedException($"Planner type {typeof(TPlanner).Name} is not supported by this runtime.");
        }

        /// <summary>Rejects work after this Map runtime has reached its cleanup boundary.</summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(MapNavigationRuntime));
        }

        /// <summary>Owns queue lifecycle state and the request metadata used before work creation.</summary>
        private abstract class PendingRequest
        {
            /// <summary>Gets the queued operation used to observe terminal completion.</summary>
            public NavigationPlanningOperation Operation { get; private set; }
            /// <summary>Gets the runtime-owned cancellation source for the queued operation.</summary>
            public CancellationTokenSource Cancellation { get; private set; }

            public abstract Vector2 BodySize { get; }
            public abstract NavigationGoalRequest Goal { get; }

            /// <summary>Creates detached planner work from this request's immutable value data.</summary>
            public abstract INavigationPlanningWork CreateWork(MapNavigationRuntime runtime);

            /// <summary>Binds queue lifecycle resources to this typed request exactly once.</summary>
            public void Initialize(NavigationPlanningOperation operation, CancellationTokenSource cancellation)
            {
                if (Operation != null || Cancellation != null)
                    throw new InvalidOperationException("Pending request lifecycle was already initialized.");
                Operation = operation ?? throw new ArgumentNullException(nameof(operation));
                Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
            }

        }

        /// <summary>Combines one typed request payload with the pending operation's queue lifecycle.</summary>
        private sealed class PendingRequest<TPlanner, TParameters> : PendingRequest
            where TPlanner : NavigationPlanner<TParameters>
            where TParameters : struct
        {
            private readonly RequestData requestData;

            public PendingRequest(AABB body, NavigationGoalRequest goal, TParameters parameters, NavigationPlanningExtent extent)
            {
                requestData = new RequestData(body, goal, parameters, extent);
            }

            public sealed override Vector2 BodySize => requestData.Body.Size;
            public sealed override NavigationGoalRequest Goal => requestData.Goal;

            public sealed override INavigationPlanningWork CreateWork(MapNavigationRuntime runtime)
            {
                TPlanner planner = runtime.GetPlanner<TPlanner>();
                return new PlannerWork<TPlanner, TParameters>(requestData.Body, requestData.Goal, planner, requestData.Parameters, requestData.Extent);
            }


            private readonly struct RequestData
            {
                public readonly AABB Body;
                public readonly NavigationGoalRequest Goal;
                public readonly TParameters Parameters;
                public readonly NavigationPlanningExtent Extent;

                public RequestData(AABB body, NavigationGoalRequest goal, TParameters parameters, NavigationPlanningExtent extent)
                {
                    Body = body;
                    Goal = goal;
                    Parameters = parameters;
                    Extent = extent;
                }
            }
        }

        /// <summary>Resolves one planning launch support against the current published NavWorld.</summary>
        public bool TryResolvePlanningGroundSupport(AABB body, out Vector2 snappedLowerCenter, out NavigationSupport support)
        {
            snappedLowerCenter = default;
            support = default;
            if (isDisposed || world == null) return false;
            return world.TryResolveGroundSupport(body,
                NavigationWorldQueries.SupportSnapDistance, out snappedLowerCenter, out support);
        }

    }
}
