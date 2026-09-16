using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI.Navigation
{
    /// <summary>Owns temporary, exact body-to-platform collision overrides for one planned jump.</summary>
    public sealed class OneWayPlatformCollisionLease : IDisposable
    {
        private const float GeometryEpsilon = NavigationConstant.Epsilon;

        private readonly Collider2D bodyCollider;
        private readonly List<Entry> entries;
        private bool enabled;

        private OneWayPlatformCollisionLease(Collider2D bodyCollider)
        {
            this.bodyCollider = bodyCollider;
            entries = new List<Entry>();
        }

        /// <summary>Gets whether at least one collision pair is still leased.</summary>
        public bool IsActive => entries.Count != 0;
        /// <summary>Gets the number of exact collider pairs still owned by this lease.</summary>
        public int EntryCount => entries.Count;

        /// <summary>Adds one exact descending support pair, preserving idempotence within geometric tolerance.</summary>
        public bool TryAddDescendingSupport(Collider2D platform, float surfaceY)
        {
            if (!IsValidOneWayCollider(platform)
                || !NavigationNumeric.IsFinite(surfaceY))
                return false;

            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.Platform != platform) continue;
                for (int crossingIndex = 0; crossingIndex < entry.Crossings.Count; crossingIndex++)
                {
                    JumpSurfaceCrossing crossing = entry.Crossings[crossingIndex];
                    if (crossing.Kind == JumpSurfaceCrossingKind.Descending
                        && Mathf.Abs(crossing.Position.y - surfaceY) <= GeometryEpsilon)
                        return true;
                }

                entry.Crossings.Add(new JumpSurfaceCrossing(default, new Vector2(0f, surfaceY), Vector2.up, 0f,
                    JumpSurfaceCrossingKind.Descending));
                if (enabled && bodyCollider && platform)
                    Physics2D.IgnoreCollision(bodyCollider, platform, true);
                return true;
            }

            entries.Add(new Entry(platform,
                new JumpSurfaceCrossing(default, new Vector2(0f, surfaceY), Vector2.up, 0f,
                    JumpSurfaceCrossingKind.Descending),
                Physics2D.GetIgnoreCollision(bodyCollider, platform)));
            if (enabled && bodyCollider && platform)
                Physics2D.IgnoreCollision(bodyCollider, platform, true);
            return true;
        }

        /// <summary>Enables all resolved pairs after the owning executor has confirmed its launch support.</summary>
        public void Enable()
        {
            if (enabled || !bodyCollider) return;
            enabled = true;
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (entry.Platform) Physics2D.IgnoreCollision(bodyCollider, entry.Platform, true);
            }
        }

        /// <summary>Restores all pairs that were changed by this lease.</summary>
        public void Restore()
        {
            for (int index = 0; index < entries.Count; index++)
            {
                Entry entry = entries[index];
                if (bodyCollider && entry.Platform && enabled)
                    Physics2D.IgnoreCollision(bodyCollider, entry.Platform, entry.WasAlreadyIgnored);
            }
            entries.Clear();
            enabled = false;
        }

        /// <summary>Restores a pair at the unique safe window derived from its complete crossing set.</summary>
        public void Tick()
        {
            if (!enabled) return;
            Vector2 anchor = NavigationBodyGeometry.GetGroundAnchor(new[] { bodyCollider });
            float bodyTopY = bodyCollider.bounds.max.y;
            float verticalVelocity = bodyCollider.attachedRigidbody ? bodyCollider.attachedRigidbody.linearVelocityY : 0f;
            for (int index = entries.Count - 1; index >= 0; index--)
            {
                Entry entry = entries[index];
                if (!bodyCollider || !entry.Platform || CanRestore(entry, bodyCollider, anchor.y, bodyTopY, verticalVelocity))
                    RestoreEntry(index, entry);
            }
        }

        /// <summary>Releases all temporary collision changes.</summary>
        public void Dispose() => Restore();

        private void Add(Collider2D platform, JumpSurfaceCrossing crossing)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Platform != platform) continue;
                for (int crossingIndex = 0; crossingIndex < entries[index].Crossings.Count; crossingIndex++)
                {
                    JumpSurfaceCrossing existing = entries[index].Crossings[crossingIndex];
                    if (existing.Kind == crossing.Kind
                        && existing.Surface == crossing.Surface
                        && Vector2.Distance(existing.Position, crossing.Position) <= GeometryEpsilon)
                        return;
                }
                entries[index].Crossings.Add(crossing);
                if (enabled && bodyCollider && platform)
                    Physics2D.IgnoreCollision(bodyCollider, platform, true);
                return;
            }

            entries.Add(new Entry(platform, crossing, Physics2D.GetIgnoreCollision(bodyCollider, platform)));
        }

        private void RestoreEntry(int index, Entry entry)
        {
            if (bodyCollider && entry.Platform && enabled)
                Physics2D.IgnoreCollision(bodyCollider, entry.Platform, entry.WasAlreadyIgnored);
            entries.RemoveAt(index);
        }





        /// <summary>Resolves planner-owned surface spans to exact current physics colliders without changing collision state.</summary>
        public static bool TryCreateForSegment(Collider2D bodyCollider, JumpRouteSegment segment, MapNavigationRuntime navigation, out OneWayPlatformCollisionLease lease)
        {
            lease = null;
            if (!bodyCollider || segment == null || navigation == null) return false;
            OneWayPlatformCollisionLease candidate = new(bodyCollider);
            IReadOnlyList<JumpSurfaceCrossing> crossings = segment.SurfaceCrossings;
            for (int index = 0; index < crossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = crossings[index];
                if (!navigation.TryResolveSource(crossing.Surface, out Collider2D platform) || !IsValidOneWayCollider(platform)) return false;
                candidate.Add(platform, crossing);
            }

            lease = candidate.IsActive ? candidate : null;
            return true;
        }

        /// <summary>Resolves a supplied immutable surface binding for low-level executor fixtures.</summary>
        public static bool TryCreateForSegment(Collider2D bodyCollider, JumpRouteSegment segment, IReadOnlyDictionary<NavigationSurfaceId, Collider2D> bindings, out OneWayPlatformCollisionLease lease)
        {
            lease = null;
            if (!bodyCollider || segment == null || bindings == null) return false;
            OneWayPlatformCollisionLease candidate = new(bodyCollider);
            IReadOnlyList<JumpSurfaceCrossing> crossings = segment.SurfaceCrossings;
            for (int index = 0; index < crossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = crossings[index];
                if (!bindings.TryGetValue(crossing.Surface, out Collider2D platform) || !IsValidOneWayCollider(platform)) return false;
                candidate.Add(platform, crossing);
            }

            lease = candidate.IsActive ? candidate : null;
            return true;
        }

        /// <summary>Creates the single appendable lease owned by one drop-through action.</summary>
        public static OneWayPlatformCollisionLease CreateForDropThrough(Collider2D bodyCollider) => bodyCollider ? new OneWayPlatformCollisionLease(bodyCollider) : null;

        private static bool CanRestore(Entry entry, Collider2D bodyCollider, float anchorY, float bodyTopY, float verticalVelocity)
        {
            bool ascendingComplete = true;
            float highestAscending = float.NegativeInfinity;
            float lowestDescending = float.PositiveInfinity;
            bool hasDescending = false;
            bool hasLanding = false;
            float landingSurface = float.PositiveInfinity;
            for (int index = 0; index < entry.Crossings.Count; index++)
            {
                JumpSurfaceCrossing crossing = entry.Crossings[index];
                switch (crossing.Kind)
                {
                    case JumpSurfaceCrossingKind.Ascending:
                        highestAscending = Mathf.Max(highestAscending, crossing.Position.y);
                        if (anchorY <= crossing.Position.y + GeometryEpsilon) ascendingComplete = false;
                        break;
                    case JumpSurfaceCrossingKind.Descending:
                        hasDescending = true;
                        lowestDescending = Mathf.Min(lowestDescending, crossing.Position.y);
                        break;
                    case JumpSurfaceCrossingKind.Landing:
                        hasLanding = true;
                        landingSurface = Mathf.Min(landingSurface, crossing.Position.y);
                        break;
                }
            }

            if (!ascendingComplete) return false;
            if (hasDescending)
            {
                if (bodyTopY >= lowestDescending - GeometryEpsilon) return false;
                ColliderDistance2D distance = bodyCollider.Distance(entry.Platform);
                return distance.distance > GeometryEpsilon;
            }
            if (hasLanding)
            {
                return verticalVelocity <= GeometryEpsilon
                    && anchorY > landingSurface + GeometryEpsilon;
            }
            return anchorY > highestAscending + GeometryEpsilon;
        }


        private static bool IsValidOneWayCollider(Collider2D collider)
        {
            if (!collider || !collider.isActiveAndEnabled || collider.isTrigger)
                return false;
            PlatformEffector2D effector = collider.GetComponent<PlatformEffector2D>();
            return effector && effector.isActiveAndEnabled && effector.useOneWay;
        }

        private sealed class Entry
        {
            public readonly Collider2D Platform;
            public readonly List<JumpSurfaceCrossing> Crossings = new();
            public readonly bool WasAlreadyIgnored;

            public Entry(Collider2D platform, JumpSurfaceCrossing crossing, bool wasAlreadyIgnored)
            {
                Platform = platform;
                Crossings.Add(crossing);
                WasAlreadyIgnored = wasAlreadyIgnored;
            }
        }
    }
}
