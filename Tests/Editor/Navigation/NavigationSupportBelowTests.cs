using System;
using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Checks exact point-to-support queries independently of standing-body clearance.</summary>
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
            NavigationWorldSnapshot translated = NavigationWorldSnapshot.Create(new Vector2(0f, 100f), 1f,
                new RectInt(-10, -10, 20, 30), new[] { Platform(1, 104f) }, Array.Empty<NavigationRegionData>());
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

        private static NavigationWorldSnapshot World(params NavigationShapeData[] shapes)
            => NavigationWorldSnapshot.Create(Vector2.zero, 1f, new RectInt(-10, -10, 20, 30),
                shapes, Array.Empty<NavigationRegionData>());

        private static NavigationShapeData Platform(int id, float y)
            => new(id, 0, NavigationShapeType.Edge, new[] { new Vector2(-5f, y), new Vector2(5f, y) },
                0f, NavigationSurfaceKind.OneWay, true, Vector2.up);

        private static NavigationShapeData Box(int id, float left, float bottom, float right, float top)
            => new(id, 0, NavigationShapeType.Polygon,
                new[] { new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top) },
                0f, NavigationSurfaceKind.Solid, true);
    }
}
