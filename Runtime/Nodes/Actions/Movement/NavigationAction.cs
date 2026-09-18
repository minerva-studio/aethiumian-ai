using System;
using System.Collections.Generic;
using System.Threading;
using Aethiumian.AI.Navigation;
using UnityEngine;

namespace Aethiumian.AI.Nodes
{
    /// <summary>Controls whether a navigation action may choose a destination in another region.</summary>
    public enum NavigationRegionPolicy
    {
        InRegion = 0,
        CrossRegion = 1,
    }

    /// <summary>
    /// Owns one navigation action's borrowed runtime, readiness and terminal cleanup.
    /// Current is captured once; permission pauses execution, never invalidation or cancellation.
    /// </summary>
    [Serializable]
    public abstract class NavigationAction : Action
    {
        private enum Phase { Ended, WaitingForWorld, Executing }
        [NonSerialized] private Phase phase;
        [NonSerialized] private CancellationTokenSource executionCancellation;
        [NonSerialized] protected IMovementSource movementSource;
        [NonSerialized] private MapNavigationRuntime navigationRuntime;
        [NonSerialized] private INavigationWorld world;
        [NonSerialized] private Rigidbody2D body;
        [NonSerialized] private Collider2D bodyCollider;
        [NonSerialized] private Collider2D[] bodyColliders;

        /// <summary>Defines whether the action's final destination must remain in its current region.</summary>
        public NavigationRegionPolicy regionPolicy = NavigationRegionPolicy.InRegion;

        /// <summary>The runtime borrowed for this run; node cleanup never disposes it.</summary>
        public MapNavigationRuntime NavigationRuntime => navigationRuntime;
        /// <summary>The immutable world bound before initialization, never a replacement runtime's world.</summary>
        protected INavigationWorld NavigationWorld => world;
        /// <summary>The physical body captured at the start of this run.</summary>
        public Rigidbody2D RigidBody => body;
        /// <summary>The primary collider captured at the start of this run.</summary>
        public Collider2D Collider => bodyCollider;
        /// <summary>The captured colliders used for navigation body geometry.</summary>
        public IReadOnlyList<Collider2D> NavigationColliders => bodyColliders;
        /// <summary>Cancelled immediately on action completion, including before the tree consumes its result.</summary>
        protected CancellationToken ExecutionCancellation => executionCancellation.Token;

        /// <summary>
        /// Checks the action's destination contract from the sampled body and the goal's target geometry.
        /// This is the canonical region rule for every <see cref="NavigationAction"/>: membership is decided
        /// by the body's center and the goal target's center, so no capability declares a region frame of
        /// its own. Both canonical points must lie inside the immutable navigation world, and the configured
        /// <see cref="NavigationRegionPolicy"/> must accept the pair.
        /// The gate answers point membership only. It does not test the whole body box, gate width,
        /// intermediate route segments, or a trajectory.
        /// </summary>
        protected bool IsNavigationDestinationAllowed(AABB body, NavigationGoalRequest goal)
        {
            if (world == null) return false;
            return IsNavigationDestinationAllowed(body.Center, goal.TargetBounds.Center);
        }

        /// <summary>
        /// Checks a destination contract between two canonical points. Ordinary destination checks use the
        /// body-and-goal overload; this overload is for positions a capability samples itself, which must
        /// pass the sampled body's center as the origin. Both points must lie inside the immutable
        /// navigation world, and the region policy applies exactly as above.
        /// </summary>
        protected bool IsNavigationDestinationAllowed(Vector2 origin, Vector2 destination)
        {
            if (world == null) return false;
            if (!world.WorldBounds.Contains(origin)) return false;
            if (!world.WorldBounds.Contains(destination)) return false;
            return regionPolicy switch
            {
                NavigationRegionPolicy.CrossRegion => true,
                NavigationRegionPolicy.InRegion => world.AreInSameRegion(origin, destination),
                _ => false,
            };
        }

        public sealed override void Awake()
        {
            phase = Phase.WaitingForWorld;
            try
            {
                movementSource = Script as IMovementSource
                    ?? throw new InvalidOperationException($"{GetType().Name} requires its control target to implement {nameof(IMovementSource)}.");
                body = gameObject.GetComponent<Rigidbody2D>();
                bodyCollider = gameObject.GetComponent<Collider2D>();
                bodyColliders = body ? NavigationBodyGeometry.GetColliders(body) : Array.Empty<Collider2D>();
                if (!body || !bodyCollider || bodyColliders.Length == 0)
                    throw new InvalidOperationException($"{GetType().Name} requires Rigidbody2D and enabled navigation colliders.");
                navigationRuntime = NavigationRuntimeContext.Current;
                if (navigationRuntime == null || navigationRuntime.IsDisposed)
                {
                    OnMissingRuntime();
                    return;
                }
                executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken);
                CaptureActionInput();
            }
            catch (Exception exception) { CompleteActionException(exception); }
        }

        // Initialization belongs to the permitted fixed-step path, including already-ready worlds.
        public sealed override void Start() { }

        public sealed override void FixedUpdate()
        {
            if (IsComplete || phase == Phase.Ended) return;
            try
            {
                if (navigationRuntime == null || navigationRuntime.IsDisposed || !body || !bodyCollider)
                {
                    CompleteAction(false);
                    return;
                }
                if (!movementSource.CanMove)
                {
                    ResetActionProgress();
                    return;
                }
                if (phase == Phase.WaitingForWorld)
                {
                    if (!navigationRuntime.TryGetWorld(out world)) return;
                    phase = Phase.Executing;
                    InitializeAction();
                    if (IsComplete || phase == Phase.Ended) return;
                }
                TickAction();
            }
            catch (Exception exception) { CompleteActionException(exception); }
        }

        public sealed override void OnDestroy() => ReleaseExecution();

        /// <summary>Captures authored inputs that must not follow a target while waiting for World readiness.</summary>
        protected virtual void CaptureActionInput() { }
        /// <summary>Initializes exactly once, after World readiness and movement permission.</summary>
        protected abstract void InitializeAction();
        /// <summary>Advances an initialized action on an allowed fixed step.</summary>
        protected abstract void TickAction();
        /// <summary>Resets observations without advancing timers while permission is withheld.</summary>
        protected virtual void ResetActionProgress() { }
        /// <summary>Settles capability-specific terminal effects before resources are released.</summary>
        protected virtual void OnActionCompleting(bool success) { }
        /// <summary>Releases owned resources, including partially initialized state; do not dispose the runtime.</summary>
        protected abstract void ReleaseActionResources();
        /// <summary>Preserves the caller's established missing-runtime failure contract.</summary>
        protected virtual void OnMissingRuntime() => throw new InvalidOperationException(
            $"{GetType().Name} requires a live NavigationRuntimeContext.Current.");

        /// <summary>Completes and releases this run before publishing its result to the behaviour tree.</summary>
        protected void CompleteAction(bool success)
        {
            if (IsComplete || phase == Phase.Ended) return;
            try { OnActionCompleting(success); }
            catch (Exception exception) { CompleteActionException(exception); return; }
            try { ReleaseExecution(); }
            catch (Exception exception) { Exception(exception); return; }
            End(success);
        }

        /// <summary>Preserves the original exception even when terminal cleanup also throws.</summary>
        protected void CompleteActionException(Exception exception)
        {
            try { ReleaseExecution(); }
            catch (Exception cleanupException) { Debug.LogException(cleanupException); }
            Exception(exception);
        }

        private void ReleaseExecution()
        {
            if (phase == Phase.Ended) return;
            phase = Phase.Ended;
            try { executionCancellation?.Cancel(); }
            finally
            {
                try { ReleaseActionResources(); }
                finally
                {
                    executionCancellation?.Dispose();
                    executionCancellation = null;
                    world = null;
                    navigationRuntime = null;
                    movementSource = null;
                    body = null;
                    bodyCollider = null;
                    bodyColliders = null;
                }
            }
        }
    }
}
