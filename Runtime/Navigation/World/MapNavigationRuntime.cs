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
        private readonly HashSet<NavigationFailureKey> failedRequests = new();
        private INavigationWorld world;
        private Dictionary<int, Collider2D> sourceColliders;
        private Exception worldBuildException;
        private bool isDisposed;

        /// <summary>Gets whether a NavWorld has been published and this runtime remains available.</summary>
        public bool IsReady => !isDisposed && world != null;

        /// <summary>Gets whether the owning world lifetime has ended.</summary>
        public bool IsDisposed => isDisposed;

        /// <summary>Gets the immutable terrain configuration shared by capture and execution.</summary>
        public NavigationPhysicsLayers PhysicsLayers { get; }

        /// <summary>Creates execution candidates even before geometry publication; invalid after disposal.</summary>
        public ContactFilter2D CreateTerrainFilter()
        {
            ThrowIfDisposed();
            return PhysicsLayers.CreateTerrainFilter();
        }

        /// <summary>Gets the compatibility snapshot revision for existing editor and coordinator diagnostics.</summary>
        public int SnapshotRevision => !isDisposed && world != null ? 1 : 0;

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

        /// <summary>Fails all waiting requests and makes later requests fail immediately with the same exception.</summary>
        public void FailWorld(Exception failure)
        {
            ThrowIfDisposed();
            if (failure == null) throw new ArgumentNullException(nameof(failure));
            if (world != null) throw new InvalidOperationException("Navigation world has already been published.");
            if (worldBuildException != null) return;
            worldBuildException = failure;
            scheduler.FailPending(failure);
            for (int i = 0; i < pendingRequests.Count; i++)
                pendingRequests[i].Operation.TryFail(failure);
            ReleaseCompletedOperations();
        }

        /// <summary>Queues ground planning with an explicit local-action or route horizon.</summary>
        public NavigationPlanningOperation PlanWalkAsync(Vector2 start, NavigationGoalRequest goalRequest,
            WalkNavigationParameters parameters, NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default, NavigationPlanningPurpose purpose = NavigationPlanningPurpose.InitialRoute)
        {
            ReleaseCompletedOperations();
            parameters = parameters.WithSupportSnapDistance(NavigationWorldQueries.SupportSnapDistance)
                .WithGroundContactTolerance(GroundTraversalEndpointPolicy.VerticalSupportTolerance);
            return QueueWork(new WalkRequestDescriptor(start, goalRequest, parameters,
                extent == NavigationPlanningExtent.NextAction, purpose), cancellationToken);
        }
        /// <summary>Queues jump planning without requiring a complete path for NextAction.</summary>
        public NavigationPlanningOperation PlanJumpAsync(Vector2 start, NavigationGoalRequest goalRequest,
            JumpNavigationParameters parameters, NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default, NavigationPlanningPurpose purpose = NavigationPlanningPurpose.InitialRoute)
        {
            ReleaseCompletedOperations();
            parameters = parameters.WithSupportSnapDistance(NavigationWorldQueries.SupportSnapDistance)
                .WithGroundContactTolerance(GroundTraversalEndpointPolicy.VerticalSupportTolerance);
            return QueueWork(new JumpRequestDescriptor(start, goalRequest, parameters, extent, purpose), cancellationToken);
        }
        /// <summary>Queues aerial planning with the same horizon contract as ground movement.</summary>
        public NavigationPlanningOperation PlanFlyAsync(Vector2 start, NavigationGoalRequest goalRequest,
            FlyNavigationParameters parameters, NavigationPlanningExtent extent = NavigationPlanningExtent.Route,
            CancellationToken cancellationToken = default, NavigationPlanningPurpose purpose = NavigationPlanningPurpose.InitialRoute)
        {
            ReleaseCompletedOperations();
            return QueueWork(new FlyRequestDescriptor(start, goalRequest, parameters, extent, purpose), cancellationToken);
        }

        /// <summary>Checks a body-clear aerial segment against the published immutable world.</summary>
        public bool IsBodyClearFlySegment(Vector2 start, Vector2 end, Vector2 bodySize)
        {
            ThrowIfDisposed();
            return world != null && world.IsCenteredBodySegmentClear(start, end, bodySize, 0.2f);
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
            failedRequests.Clear();
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

        /// <summary>Creates pure planner work on the main thread and queues only its detached object graph.</summary>
        private NavigationPlanningOperation QueueWork(PlanningRequestDescriptor descriptor, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ReleaseCompletedOperations();
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (worldBuildException != null) return NavigationPlanningOperation.CreateFailed(worldBuildException);
            CancellationTokenSource ownedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                NavigationPlanningOperation operation = new();
                operation.RegisterCancellation(ownedCancellation.Token);
                PendingRequest request = new(operation, ownedCancellation, descriptor);
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

        /// <summary>Constructs a request's worker-owned planner graph before it enters the background queue.</summary>
        private void Schedule(PendingRequest request)
        {
            if (request.IsScheduled || request.Operation.IsCompleted) return;
            if (request.Operation.IsCancellationRequested)
            {
                request.Operation.TryFinalizeCancellation();
                return;
            }
            try
            {
                NavigationGoalRegion goalRegion = request.Descriptor.Bind(world);
                NavigationProfileKey profileKey = request.Descriptor.ProfileKey;
                if (!CanPossiblyReachGoal(goalRegion, request.Descriptor.BodySize, world))
                {
                    request.Operation.TryComplete(NavigationPlanResult.NoResult);
                    request.IsScheduled = true;
                    return;
                }

                if (!TryCreateFailureKey(request.Descriptor.Start, goalRegion, profileKey,
                    request.Descriptor.Purpose, out NavigationFailureKey failureKey))
                {
                    request.Operation.TryComplete(NavigationPlanResult.NoResult);
                    request.IsScheduled = true;
                    return;
                }
                request.AssignFailureKey(failureKey);
                if (failedRequests.Contains(failureKey))
                {
                    request.Operation.TryComplete(NavigationPlanResult.SearchExhausted());
                    request.IsScheduled = true;
                    return;
                }

                INavigationPlanningWork work = request.TakeDescriptor().CreateWork(this, goalRegion);
                scheduler.PlanWork(work, request.Cancellation.Token, request.Operation);
                request.IsScheduled = true;
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
            DrainCompletedPlanningResults();
            for (int i = pendingRequests.Count - 1; i >= 0; i--)
            {
                PendingRequest request = pendingRequests[i];
                if (!request.Operation.IsCompleted) continue;
                NavigationPlanResult result = request.Operation.PlanResult;
                if (request.FailureKey.HasValue && request.Operation.Exception == null
                    && !request.Operation.IsCancelled && result.Route == null
                    && result.Termination == NavigationPlanTermination.SearchExhausted)
                {
                    failedRequests.Add(request.FailureKey.Value);
                }
                request.Operation.ReleaseCancellationRegistration();
                request.Cancellation.Dispose();
                pendingRequests.RemoveAt(i);
            }
        }

        /// <summary>Drains worker completion notifications after operations publish their own results.</summary>
        private void DrainCompletedPlanningResults()
        {
            while (scheduler.TryDequeueCompletion(out _)) { }
        }


        /// <summary>Rejects work after this Map runtime has reached its cleanup boundary.</summary>
        private void ThrowIfDisposed()
        {
            if (isDisposed) throw new ObjectDisposedException(nameof(MapNavigationRuntime));
        }

        /// <summary>
        /// Conservatively rejects only goals no body AABB can touch from inside the finite world.
        /// </summary>
        public static bool CanPossiblyReachGoal(NavigationGoalRegion goalRegion, Vector2 bodySize, INavigationWorld snapshot)
        {
            if (goalRegion == null) throw new ArgumentNullException(nameof(goalRegion));
            if (snapshot == null) return true;
            if (goalRegion.IsRetreat) return true;
            Bounds goal = goalRegion.IsGroundWalk
                ? goalRegion.GetLowerCenterAcceptanceBounds(bodySize.x)
                : goalRegion.TargetBounds;
            float expansion = Mathf.Max(bodySize.x, bodySize.y) + goalRegion.ArrivalErrorBound;
            goal.Expand(expansion * 2f);
            Bounds worldBounds = new(
                snapshot.Origin + new Vector2(snapshot.CellBounds.center.x, snapshot.CellBounds.center.y) * snapshot.CellSize,
                new Vector2(snapshot.CellBounds.width, snapshot.CellBounds.height) * snapshot.CellSize);
            return goal.Intersects(worldBounds);
        }

        /// <summary>Owns cancellation resources until one queued operation reaches a terminal outcome.</summary>
        private sealed class PendingRequest
        {
            /// <summary>Gets the queued operation used to observe terminal completion.</summary>
            public NavigationPlanningOperation Operation { get; }
            /// <summary>Gets the runtime-owned cancellation source for the queued operation.</summary>
            public CancellationTokenSource Cancellation { get; }
            /// <summary>Gets the immutable deduplication key for an ordinary failed request, when available.</summary>
            public NavigationFailureKey? FailureKey { get; private set; }
            private PlanningRequestDescriptor descriptor;
            /// <summary>Gets or sets whether this request has entered the background queue.</summary>
            public bool IsScheduled { get; set; }
            private bool hasFailureKey;
            /// <summary>Creates one runtime-owned pending request record.</summary>
            public PendingRequest(NavigationPlanningOperation operation, CancellationTokenSource cancellation,
                PlanningRequestDescriptor descriptor)
            {
                Operation = operation;
                Cancellation = cancellation;
                this.descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
            }

            /// <summary>Assigns the snapshot-derived failure key exactly once during Schedule binding.</summary>
            public void AssignFailureKey(NavigationFailureKey? failureKey)
            {
                if (hasFailureKey) throw new InvalidOperationException("A planning failure key was already assigned.");
                FailureKey = failureKey;
                hasFailureKey = true;
            }

            /// <summary>Gets the still-owned descriptor before detached work is created.</summary>
            public PlanningRequestDescriptor Descriptor
                => descriptor ?? throw new InvalidOperationException("Planning work was already constructed.");

            /// <summary>Transfers the strongly typed request exactly once before queue submission.</summary>
            public PlanningRequestDescriptor TakeDescriptor()
            {
                PlanningRequestDescriptor result = descriptor;
                descriptor = null;
                return result ?? throw new InvalidOperationException("Planning work was already constructed.");
            }
        }

        /// <summary>Stores pure request data until a published world can create detached planner work.</summary>
        private abstract class PlanningRequestDescriptor
        {
            public abstract Vector2 Start { get; }
            public abstract Vector2 BodySize { get; }
            public abstract NavigationProfileKey ProfileKey { get; }
            public abstract NavigationPlanningPurpose Purpose { get; }
            public abstract NavigationGoalRegion Bind(INavigationWorld snapshot);
            public abstract INavigationPlanningWork CreateWork(MapNavigationRuntime runtime,
                NavigationGoalRegion goalRegion);
        }

        private sealed class WalkRequestDescriptor : PlanningRequestDescriptor
        {
            private readonly Vector2 start;
            private readonly NavigationGoalRequest goalRequest;
            private readonly WalkNavigationParameters parameters;
            private readonly bool simple;
            private readonly NavigationPlanningPurpose purpose;

            public WalkRequestDescriptor(Vector2 start, NavigationGoalRequest goalRequest,
                WalkNavigationParameters parameters, bool simple, NavigationPlanningPurpose purpose)
            {
                this.start = start;
                this.goalRequest = goalRequest;
                this.parameters = parameters;
                this.simple = simple;
                this.purpose = purpose;
            }

            public override Vector2 Start => start;
            public override Vector2 BodySize => parameters.BodySize;
            public override NavigationProfileKey ProfileKey => ProfileKeyFor(parameters, simple ? 4 : 1);
            public override NavigationPlanningPurpose Purpose => purpose;
            public override NavigationGoalRegion Bind(INavigationWorld snapshot)
                => NavigationGoalRegion.Bind(goalRequest, snapshot);

            public override INavigationPlanningWork CreateWork(MapNavigationRuntime runtime,
                NavigationGoalRegion boundGoalRegion)
            {
                WalkNavigationPlanner planner = runtime.walkPlanner
                    ?? throw new InvalidOperationException("Walk planner is unavailable before world publication.");
                return new PlannerWork(cancellationToken => simple
                    ? planner.PlanSingleStep(start, boundGoalRegion, parameters, cancellationToken)
                    : planner.Plan(start, boundGoalRegion, parameters, cancellationToken));
            }
        }

        private sealed class JumpRequestDescriptor : PlanningRequestDescriptor
        {
            private readonly Vector2 start;
            private readonly NavigationGoalRequest goalRequest;
            private readonly JumpNavigationParameters parameters;
            private readonly NavigationPlanningPurpose purpose;
            private readonly NavigationPlanningExtent extent;

            public JumpRequestDescriptor(Vector2 start, NavigationGoalRequest goalRequest,
                JumpNavigationParameters parameters, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose)
            {
                this.start = start;
                this.goalRequest = goalRequest;
                this.parameters = parameters;
                this.extent = extent;
                this.purpose = purpose;
            }

            public override Vector2 Start => start;
            public override Vector2 BodySize => parameters.BodySize;
            public override NavigationProfileKey ProfileKey => ProfileKeyFor(parameters, extent == NavigationPlanningExtent.NextAction ? 5 : 2);
            public override NavigationPlanningPurpose Purpose => purpose;
            public override NavigationGoalRegion Bind(INavigationWorld snapshot) => NavigationGoalRegion.Bind(goalRequest, snapshot);

            public override INavigationPlanningWork CreateWork(MapNavigationRuntime runtime,
                NavigationGoalRegion boundGoalRegion)
            {
                JumpNavigationPlanner planner = runtime.jumpPlanner
                    ?? throw new InvalidOperationException("Jump planner is unavailable before world publication.");
                return new PlannerWork(cancellationToken => extent == NavigationPlanningExtent.NextAction
                    ? planner.PlanSingleStep(start, boundGoalRegion, parameters, cancellationToken)
                    : planner.Plan(start, boundGoalRegion, parameters, cancellationToken));
            }
        }

        private sealed class FlyRequestDescriptor : PlanningRequestDescriptor
        {
            private readonly Vector2 start;
            private readonly NavigationGoalRequest goalRequest;
            private readonly FlyNavigationParameters parameters;
            private readonly NavigationPlanningPurpose purpose;
            private readonly NavigationPlanningExtent extent;

            public FlyRequestDescriptor(Vector2 start, NavigationGoalRequest goalRequest,
                FlyNavigationParameters parameters, NavigationPlanningExtent extent, NavigationPlanningPurpose purpose)
            {
                this.start = start;
                this.goalRequest = goalRequest;
                this.parameters = parameters;
                this.extent = extent;
                this.purpose = purpose;
            }

            public override Vector2 Start => start;
            public override Vector2 BodySize => parameters.BodySize;
            public override NavigationProfileKey ProfileKey => ProfileKeyFor(parameters, extent == NavigationPlanningExtent.NextAction ? 6 : 3);
            public override NavigationPlanningPurpose Purpose => purpose;
            public override NavigationGoalRegion Bind(INavigationWorld snapshot) => NavigationGoalRegion.Bind(goalRequest, snapshot);

            public override INavigationPlanningWork CreateWork(MapNavigationRuntime runtime,
                NavigationGoalRegion boundGoalRegion)
            {
                FlyNavigationPlanner planner = runtime.flyPlanner
                    ?? throw new InvalidOperationException("Fly planner is unavailable before world publication.");
                return new PlannerWork(cancellationToken => extent == NavigationPlanningExtent.NextAction
                    ? planner.PlanSingleStep(start, boundGoalRegion, parameters, cancellationToken)
                    : planner.Plan(start, boundGoalRegion, parameters, cancellationToken));
            }
        }

        /// <summary>Returns the cell rectangle covered by a goal region in the published snapshot.</summary>
        internal RectInt GetGoalRegionCellBounds(NavigationGoalRegion goalRegion) => GetGoalRegionCellBounds(goalRegion, Vector2.zero);

        /// <summary>Returns the cell rectangle covered by a goal using the supplied body width for Ground Walk.</summary>
        internal RectInt GetGoalRegionCellBounds(NavigationGoalRegion goalRegion, Vector2 bodySize)
        {
            ThrowIfDisposed();
            if (goalRegion == null) throw new ArgumentNullException(nameof(goalRegion));
            if (world == null) return new RectInt();
            Bounds bounds = goalRegion.IsGroundWalk && bodySize.x > 0f
                ? goalRegion.GetLowerCenterAcceptanceBounds(bodySize.x)
                : goalRegion.TargetBounds;
            int minX = Mathf.FloorToInt((bounds.min.x - world.Origin.x) / world.CellSize);
            int minY = Mathf.FloorToInt((bounds.min.y - world.Origin.y) / world.CellSize);
            int maxX = bounds.size.x == 0f
                ? minX
                : Mathf.CeilToInt((bounds.max.x - world.Origin.x) / world.CellSize) - 1;
            int maxY = bounds.size.y == 0f
                ? minY
                : Mathf.CeilToInt((bounds.max.y - world.Origin.y) / world.CellSize) - 1;
            int xMin = Mathf.Max(minX, world.CellBounds.xMin);
            int yMin = Mathf.Max(minY, world.CellBounds.yMin);
            int xMax = Mathf.Min(maxX, world.CellBounds.xMax - 1);
            int yMax = Mathf.Min(maxY, world.CellBounds.yMax - 1);
            return xMax < xMin || yMax < yMin
                ? new RectInt()
                : new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
        }

        /// <summary>Gets the immutable cell size used by the published navigation snapshot.</summary>
        public float CellSize => world?.CellSize ?? 1f;

        /// <summary>Resolves one planning launch support against the current published NavWorld.</summary>
        public bool TryResolvePlanningGroundSupport(Vector2 observedLowerCenter, Vector2 bodySize, out Vector2 snappedLowerCenter, out NavigationSupport support)
        {
            snappedLowerCenter = default;
            support = default;
            if (isDisposed || world == null) return false;
            return world.TryResolveGroundSupport(observedLowerCenter, bodySize,
                NavigationWorldQueries.SupportSnapDistance, out snappedLowerCenter, out support);
        }

        /// <summary>Returns the cell containing a planning anchor in the published snapshot.</summary>
        internal Vector2Int GetPlanningStartCell(Vector2 position)
        {
            ThrowIfDisposed();
            return world == null ? default : NavigationWorldQueries.WorldToCell(world, position);
        }

        private bool TryCreateFailureKey(Vector2 start, NavigationGoalRegion goalRegion,
            NavigationProfileKey profileKey, NavigationPlanningPurpose purpose, out NavigationFailureKey key)
        {
            key = default;
            if (world == null || goalRegion == null) return false;
            Vector2 snappedStart = default;
            NavigationSupport support = default;
            bool hasSupport = profileKey.IsGround && world.TryResolveGroundSupport(start, profileKey.BodySize, out snappedStart, out support);
            key = new NavigationFailureKey(hasSupport, support.Surface, hasSupport ? snappedStart : start,
                goalRegion, profileKey, purpose);
            return true;
        }

        private static NavigationProfileKey ProfileKeyFor(WalkNavigationParameters parameters, int kind) => ProfileKey(parameters, kind);

        private static NavigationProfileKey ProfileKeyFor(JumpNavigationParameters parameters, int kind) => ProfileKey(parameters, kind);

        private static NavigationProfileKey ProfileKeyFor(FlyNavigationParameters parameters, int kind) => ProfileKey(parameters, kind);

        private static NavigationProfileKey ProfileKey(WalkNavigationParameters parameters, int kind)
            => CombineProfile(kind, parameters.BodySize, parameters.Speed, parameters.SupportSnapDistance, parameters.Gravity,
                parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight, parameters.JumpLength, parameters.SimulationTimeStep,
                parameters.GroundContactTolerance);

        private static NavigationProfileKey ProfileKey(JumpNavigationParameters parameters, int kind)
            => CombineProfile(kind, parameters.BodySize, 0f, parameters.SupportSnapDistance, parameters.Gravity,
                parameters.GravityScale, parameters.LinearDamping, parameters.JumpHeight, parameters.JumpLength, parameters.SimulationTimeStep,
                parameters.GroundContactTolerance);

        private static NavigationProfileKey ProfileKey(FlyNavigationParameters parameters, int kind)
            => CombineProfile(kind, parameters.BodySize, 0f, 0f, Vector2.zero, 0f, 0f, 0f, 0f, 0f,
                parameters.HasApproachLimit ? parameters.RemainingApproachDistance : -1f);

        private static NavigationProfileKey CombineProfile(int kind, Vector2 bodySize, float walkSpeed, float supportSnap,
            Vector2 gravity, float gravityScale, float damping, float jumpHeight, float jumpLength, float timestep,
            float groundContactTolerance)
            => new(kind, bodySize, walkSpeed, supportSnap, gravity, gravityScale, damping, jumpHeight, jumpLength, timestep,
                groundContactTolerance);

        private readonly struct NavigationFailureKey : IEquatable<NavigationFailureKey>
        {
            private readonly bool hasSupport;
            private readonly NavigationSurfaceId supportSurface;
            private readonly int startX;
            private readonly int startY;
            private readonly NavigationGoalKey goal;
            private readonly NavigationProfileKey profileKey;
            private readonly NavigationPlanningPurpose purpose;

            public NavigationProfileKey ProfileKey => profileKey;
            public NavigationFailureKey(bool hasSupport, NavigationSurfaceId supportSurface, Vector2 start,
                NavigationGoalRegion goalRegion, NavigationProfileKey profileKey, NavigationPlanningPurpose purpose)
            {
                this.hasSupport = hasSupport;
                this.supportSurface = supportSurface;
                startX = BitConverter.SingleToInt32Bits(start.x);
                startY = BitConverter.SingleToInt32Bits(start.y);
                goal = goalRegion.GoalKey;
                this.profileKey = profileKey;
                this.purpose = purpose;
            }
            public bool Equals(NavigationFailureKey other) => hasSupport == other.hasSupport
                && supportSurface == other.supportSurface
                && startX == other.startX
                && startY == other.startY
                && goal.Equals(other.goal)
                && profileKey.Equals(other.profileKey)
                && purpose == other.purpose;
            public override bool Equals(object obj) => obj is NavigationFailureKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(hasSupport, supportSurface, startX, startY,
                goal, profileKey, purpose);
        }

        /// <summary>Stores exact profile value bits used to generate a navigation search.</summary>
        private readonly struct NavigationProfileKey : IEquatable<NavigationProfileKey>
        {
            private readonly int kind;
            private readonly int bodyWidth;
            private readonly int bodyHeight;
            private readonly int walkSpeed;
            private readonly int value0;
            private readonly int gravityX;
            private readonly int gravityY;
            private readonly int value1;
            private readonly int value2;
            private readonly int value3;
            private readonly int value4;
            private readonly int value5;
            private readonly int value6;

            public bool IsGround => kind == 1 || kind == 2 || kind == 4;
            public bool IsSmartWalk => kind == 1;
            public float WalkSpeed => BitConverter.Int32BitsToSingle(walkSpeed);
            public float SimulationTimeStep => BitConverter.Int32BitsToSingle(value5);
            public Vector2 BodySize => new(
                BitConverter.Int32BitsToSingle(bodyWidth), BitConverter.Int32BitsToSingle(bodyHeight));

            /// <summary>Creates an exact value key from one planner profile.</summary>
            public NavigationProfileKey(int kind, Vector2 bodySize, float walkSpeed, float value0, Vector2 gravity, float value1, float value2, float value3, float value4, float value5, float value6)
            {
                this.kind = kind;
                this.walkSpeed = BitConverter.SingleToInt32Bits(walkSpeed);
                this.bodyWidth = BitConverter.SingleToInt32Bits(bodySize.x);
                this.bodyHeight = BitConverter.SingleToInt32Bits(bodySize.y);
                this.value0 = BitConverter.SingleToInt32Bits(value0);
                this.gravityX = BitConverter.SingleToInt32Bits(gravity.x);
                this.gravityY = BitConverter.SingleToInt32Bits(gravity.y);
                this.value1 = BitConverter.SingleToInt32Bits(value1);
                this.value2 = BitConverter.SingleToInt32Bits(value2);
                this.value3 = BitConverter.SingleToInt32Bits(value3);
                this.value4 = BitConverter.SingleToInt32Bits(value4);
                this.value5 = BitConverter.SingleToInt32Bits(value5);
                this.value6 = BitConverter.SingleToInt32Bits(value6);
            }

            /// <summary>Compares every exact profile value rather than relying on a hash.</summary>
            public bool Equals(NavigationProfileKey other)
                => kind == other.kind
                    && walkSpeed == other.walkSpeed
                    && bodyWidth == other.bodyWidth
                    && bodyHeight == other.bodyHeight
                    && value0 == other.value0
                    && gravityX == other.gravityX
                    && gravityY == other.gravityY
                    && value1 == other.value1
                    && value2 == other.value2
                    && value3 == other.value3
                    && value4 == other.value4
                    && value5 == other.value5
                    && value6 == other.value6;

            public override bool Equals(object obj) => obj is NavigationProfileKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = kind;
                    hash = hash * 31 + walkSpeed;
                    hash = hash * 31 + bodyWidth;
                    hash = hash * 31 + bodyHeight;
                    hash = hash * 31 + value0;
                    hash = hash * 31 + gravityX;
                    hash = hash * 31 + gravityY;
                    hash = hash * 31 + value1;
                    hash = hash * 31 + value2;
                    hash = hash * 31 + value3;
                    hash = hash * 31 + value4;
                    hash = hash * 31 + value5;
                    return hash * 31 + value6;
                }
            }
        }
    }
}
