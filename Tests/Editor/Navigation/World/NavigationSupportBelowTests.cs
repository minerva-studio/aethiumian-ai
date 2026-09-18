using System;
using System.Collections.Generic;
using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>
    /// Checks exact point-to-support queries independently of standing-body clearance.
    /// </summary>
    public sealed class NavigationSupportBelowTests
    {
        [Test]
        public void NearestSupport_IncludesPlatformsAndIgnoresSurfacesAbovePoint()
        {
            NavigationWorldSnapshot world = World(
                Box(1, -5f, 0f, 5f, 1f), Platform(2, 4f), Platform(3, 8f));
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 6f), out NavigationSupport support), Is.True);
            Assert.That(support.Surface.SourceId, Is.EqualTo(2));
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(support.Position.y, Is.EqualTo(4f));
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 3f), out support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(1f));
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 4f), out support), Is.True);
            Assert.That(support.Surface.SourceId, Is.EqualTo(2));
        }

        [Test]
        public void NoSupport_DoesNotInventFloorOrUsePolygonUnderside()
        {
            NavigationWorldSnapshot world = World(Box(1, -5f, 3f, 5f, 5f));
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 4f), out _), Is.False);
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 2f), out _), Is.False);
            Assert.That(world.TryGetSupportBelow(new Vector2(12f, 8f), out _), Is.False);
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, -11f), out _), Is.False);
        }

        [Test]
        public void InvalidUpperOneWayEdge_DoesNotHideAllowedLowerEdgeOfSameShape()
        {
            // The upper slope's normal is outside the narrow upward one-way arc.
            NavigationShapeData folded = new(2, 0, NavigationShapeType.Edge,
                new[] { new Vector2(-2f, 2f), new Vector2(2f, 2f),
                    new Vector2(2f, 8f), new Vector2(-2f, 4f) },
                0f, NavigationSurfaceKind.OneWay, true, Vector2.up, 0.99f);
            NavigationWorldSnapshot world = World(Box(1, -5f, -1f, 5f, 0f), folded);
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 10f), out NavigationSupport support), Is.True);
            Assert.That(support.Surface.SourceId, Is.EqualTo(2));
            Assert.That(support.Position.y, Is.EqualTo(2f));
        }

        [Test]
        public void DisabledOrDownwardPlatform_DoesNotHideLowerSupport()
        {
            NavigationShapeData disabled = new(3, 0, NavigationShapeType.Edge,
                new[] { new Vector2(-5f, 7f), new Vector2(5f, 7f) }, 0f, NavigationSurfaceKind.Solid, false);
            NavigationShapeData downward = new(2, 0, NavigationShapeType.Edge,
                new[] { new Vector2(-5f, 5f), new Vector2(5f, 5f) },
                0f, NavigationSurfaceKind.OneWay, true, Vector2.down);
            NavigationWorldSnapshot world = World(Box(1, -5f, 0f, 5f, 1f), downward, disabled);
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 9f), out NavigationSupport support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(1f));
        }

        [Test]
        public void DirectedCompositeContours_IgnoreInnerDownwardTopAndPreserveMirror()
        {
            foreach (bool mirrored in new[] { false, true })
            {
                NavigationWorldSnapshot world = DirectedCompositeWorld(mirrored);
                float x = mirrored ? -0.5f : 0.5f;
                Assert.That(world.TryGetSupportBelow(new Vector2(x, 15.99f), out NavigationSupport support), Is.True);
                Assert.That(support.Surface, Is.EqualTo(new NavigationSurfaceId(17, 9)));
                Assert.That(support.Position.y, Is.EqualTo(15f).Within(0.0001f));

                List<NavigationSupportCandidate> candidates = new();
                world.CollectSupportCandidates(AABB.FromMinAndSize(x - 0.75f, 14.9f, 1.5f, 1.2f), new Vector2(0.5f, 1f), candidates);
                for (int index = 0; index < candidates.Count; index++)
                    Assert.That(candidates[index].Support.Position.y, Is.Not.EqualTo(15.9687f).Within(0.0001f));

                List<NavigationSurfaceCrossing> crossings = new();
                world.CollectOneWayCrossings(AABB.FromLowerCenter(new Vector2(x, 15.99f), new Vector2(0.5f, 0f)),
                    new Vector2(x, 15.5f) - new Vector2(x, 15.99f), crossings);
                Assert.That(crossings, Is.Empty);
            }
        }

        [Test]
        public void DirectedCompositeContours_RetainSameSourceLowerPlanningCandidate()
        {
            NavigationWorldSnapshot world = DirectedCompositeWorld(false);
            Vector2 lowerAnchor = new(3f, 9f);
            Assert.That(world.CanStandAt(AABB.FromLowerCenter(lowerAnchor, new Vector2(0.5f, 1f)), out bool isOneWay), Is.True);
            Assert.That(isOneWay, Is.True);

            List<NavigationSupportCandidate> candidates = new();
            world.CollectSupportCandidates(AABB.FromMinAndSize(2.25f, 8.5f, 1.5f, 1f), new Vector2(0.5f, 1f), candidates);
            bool foundLower = false;
            for (int index = 0; index < candidates.Count; index++)
            {
                NavigationSupport support = candidates[index].Support;
                if (support.Surface == new NavigationSurfaceId(17, 10)
                    && Mathf.Abs(support.Position.y - 9f) <= 0.0001f)
                    foundLower = true;
            }

            Assert.That(foundLower, Is.True);
        }

        [Test]
        public void SlopeAndCurves_AreEvaluatedAtExactX()
        {
            NavigationShapeData slope = new(1, 0, NavigationShapeType.Polygon,
                new[] { new Vector2(-2f, 0f), new Vector2(2f, 0f), new Vector2(2f, 4f) },
                0f, NavigationSurfaceKind.Solid, true);
            Assert.That(World(slope).TryGetSupportBelow(new Vector2(0.25f, 8f), out NavigationSupport support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(2.25f).Within(0.0001f));
            NavigationShapeData circle = new(2, 0, NavigationShapeType.Circle,
                new[] { new Vector2(0f, 2f) }, 2f, NavigationSurfaceKind.Solid, true);
            Assert.That(World(circle).TryGetSupportBelow(new Vector2(1f, 8f), out support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(2f + Mathf.Sqrt(3f)).Within(0.0001f));
            NavigationShapeData capsule = new(3, 0, NavigationShapeType.Capsule,
                new[] { new Vector2(-2f, 2f), new Vector2(2f, 2f) }, 1f, NavigationSurfaceKind.Solid, true);
            Assert.That(World(capsule).TryGetSupportBelow(new Vector2(0f, 8f), out support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void EqualHeightTie_IsIndependentOfCaptureOrder()
        {
            foreach (NavigationShapeData[] shapes in new[] {
                new[] { Platform(9, 4f), Platform(2, 4f) },
                new[] { Platform(2, 4f), Platform(9, 4f) } })
            {
                Assert.That(World(shapes).TryGetSupportBelow(new Vector2(0f, 9f), out NavigationSupport support), Is.True);
                Assert.That(support.Surface.SourceId, Is.EqualTo(2));
            }
        }

        [Test]
        public void VerticalTranslation_PreservesRelativeHeight()
        {
            NavigationWorldSnapshot translated = NavigationWorldSnapshot.Create(AABB.FromMinAndSize(-10f, 90f, 20f, 30f),
                new[] { Platform(1, 104f) }, Array.Empty<NavigationRegionData>());
            Assert.That(World(Platform(1, 4f)).TryGetSupportBelow(new Vector2(0f, 9f), out NavigationSupport first), Is.True);
            Assert.That(translated.TryGetSupportBelow(new Vector2(0f, 109f), out NavigationSupport second), Is.True);
            Assert.That(9f - first.Position.y, Is.EqualTo(109f - second.Position.y));
        }

        [Test]
        public void Query_DoesNotRequireStandingClearanceAndRejectsNonFiniteInput()
        {
            NavigationWorldSnapshot world = World(Platform(1, 4f), Box(2, -5f, 4.1f, 5f, 5f));
            Assert.That(world.TryGetSupportBelow(new Vector2(0f, 4.05f), out NavigationSupport support), Is.True);
            Assert.That(support.Position.y, Is.EqualTo(4f));
            Assert.That(() => world.TryGetSupportBelow(new Vector2(float.NaN, 0f), out _),
                Throws.InstanceOf<ArgumentException>());
        }

        /// <summary>Verifies support identity survives snapshot capture for an authored one-way edge.</summary>
        [Test]
        public void SnapshotResolvesImmutableSupportIdentity()
        {
            NavigationSurfaceId surface = new(7, 3);
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 4, 4),
                new[]
                {
                    new NavigationShapeData(7, 3, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(3f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, directedNormalSign: 0f),
                },
                new[] { new NavigationRegionData(AABB.FromMinAndSize(0, 0, 4, 4), 11) });

            Assert.That(world.TryResolveSupport(AABB.FromLowerCenter(new Vector2(1.25f, 1.01f), new Vector2(0.8f, 1.2f)),
                0.1f, out NavigationSupport support), Is.True);
            Assert.That(support.Surface, Is.EqualTo(surface));
            Assert.That(support.Kind, Is.EqualTo(NavigationSurfaceKind.OneWay));
            Assert.That(support.Position, Is.EqualTo(new Vector2(1.25f, 1f)));
            Assert.That(world.AreInSameRegion(new Vector2(0.1f, 0.1f), new Vector2(3.9f, 3.9f)), Is.True);
        }

        /// <summary>Verifies one-way crossing records retain source identity and monotonic fractions.</summary>
        [Test]
        public void SnapshotReportsOneWayCrossingProvenance()
        {
            NavigationSurfaceId surface = new(2, 5);
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 4, 4),
                new[]
                {
                    new NavigationShapeData(2, 5, NavigationShapeType.Edge,
                        new[] { new Vector2(0f, 1f), new Vector2(3f, 1f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, directedNormalSign: 0f),
                }, Array.Empty<NavigationRegionData>());
            List<NavigationSurfaceCrossing> crossings = new();

            world.CollectOneWayCrossings(AABB.FromLowerCenter(new Vector2(1.5f, 2f), new Vector2(0.8f, 0f)),
                new Vector2(1.5f, 0.5f) - new Vector2(1.5f, 2f), crossings);

            Assert.That(crossings, Has.Count.EqualTo(3));
            for (int index = 0; index < crossings.Count; index++)
            {
                Assert.That(crossings[index].Surface, Is.EqualTo(surface));
                Assert.That(crossings[index].Position.y, Is.EqualTo(1f).Within(0.001f));
                if (index > 0) Assert.That(crossings[index].Fraction, Is.GreaterThanOrEqualTo(crossings[index - 1].Fraction));
            }
        }

        /// <summary>Verifies one-way crossing uses the captured non-integer surface height.</summary>
        [Test]
        public void OneWayQueryUsesCapturedNonIntegerSurfaceHeight()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                AABB.FromMinAndSize(0, 0, 3, 8),
                new[]
                {
                    new NavigationShapeData(1, 0, NavigationShapeType.Edge,
                        new[] { new Vector2(1f, 5.38f), new Vector2(2f, 5.38f) }, 0f,
                        NavigationSurfaceKind.OneWay, true, Vector2.up, 0.8f, directedNormalSign: 0f),
                }, Array.Empty<NavigationRegionData>());

            Assert.That(world.CrossesOneWayDown(AABB.FromLowerCenter(new Vector2(1.5f, 5.5f), new Vector2(0.8f, 0f)),
                new Vector2(1.5f, 5.2f) - new Vector2(1.5f, 5.5f)), Is.True);
            Assert.That(world.CrossesOneWayDown(AABB.FromLowerCenter(new Vector2(1.5f, 5.5f), new Vector2(0.8f, 0f)),
                new Vector2(1.5f, 5.39f) - new Vector2(1.5f, 5.5f)), Is.False);
        }

        private static NavigationWorldSnapshot World(params NavigationShapeData[] shapes)
            => NavigationWorldSnapshot.Create(AABB.FromMinAndSize(-10, -10, 20, 30),
                shapes, Array.Empty<NavigationRegionData>());

        private static NavigationShapeData Platform(int id, float y)
            => new(id, 0, NavigationShapeType.Edge, new[] { new Vector2(-5f, y), new Vector2(5f, y) },
                0f, NavigationSurfaceKind.OneWay, true, Vector2.up);

        private static NavigationWorldSnapshot DirectedCompositeWorld(bool mirrored)
        {
            float sign = mirrored ? -1f : 1f;
            float MirrorX(float x) => mirrored ? -x : x;
            Vector2 Point(float x, float y) => new(MirrorX(x), y);
            return World(
                new NavigationShapeData(17, 7, NavigationShapeType.Edge,
                    new[] { Point(-4f, 12f), Point(4f, 12f), Point(4f, 16f), Point(-4f, 16f), Point(-4f, 12f) },
                    0f, NavigationSurfaceKind.OneWay, true, Vector2.up, directedNormalSign: sign),
                new NavigationShapeData(17, 9, NavigationShapeType.Edge,
                    new[] { Point(-1f, 15f), Point(-1f, 15.9687f), Point(1f, 15.9687f), Point(1f, 15f), Point(-1f, 15f) },
                    0f, NavigationSurfaceKind.OneWay, true, Vector2.up, directedNormalSign: sign),
                new NavigationShapeData(17, 10, NavigationShapeType.Edge,
                    new[] { Point(-4f, 7f), Point(4f, 7f), Point(4f, 9f), Point(-4f, 9f), Point(-4f, 7f) },
                    0f, NavigationSurfaceKind.OneWay, true, Vector2.up, directedNormalSign: sign));
        }

        private static NavigationShapeData Box(int id, float left, float bottom, float right, float top)
            => new(id, 0, NavigationShapeType.Polygon,
                new[] { new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top) },
                0f, NavigationSurfaceKind.Solid, true);
    }
}
