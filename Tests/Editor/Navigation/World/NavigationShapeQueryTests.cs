using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    public sealed class NavigationShapeQueryTests
    {
        [Test]
        public void InteriorBucketQueriesDetectWideShapesAndPreserveClearResults()
        {
            NavigationWorldSnapshot world = World(Polygon(1, -1f, 0.25f, 3.75f, 0.75f));
            Assert.That(world.IsBodyClear(AABB.FromMinAndSize(2.2f, 0.3f, 0.2f, 0.2f), 0f), Is.False);
            Assert.That(world.IsLineOfSightClear(new Vector2(2.2f, 0.1f), new Vector2(2.2f, 0.9f)), Is.False);
            Assert.That(world.IsBodyClear(AABB.FromMinAndSize(2.2f, 2f, 0.2f, 0.2f), 0f), Is.True);
            Assert.That(world.IsLineOfSightClear(new Vector2(2f, 2f), new Vector2(3f, 2f)), Is.True);
            Assert.That(world.IsBodyClear(AABB.FromMinAndSize(-1f, 0f, 0.2f, 0.2f), 0f), Is.False);
            Assert.That(World().IsBodyClear(AABB.FromMinAndSize(0f, 0f, 4f, 4f), 0f), Is.True);
        }

        [Test]
        public void SupportNearTolerancePreservesFirstEncounterSelection()
        {
            // Successively lower surfaces win epsilon ties by identity. Revisiting the
            // first surface after the third would incorrectly select the first again.
            float epsilon = NavigationConstant.Epsilon;
            NavigationWorldSnapshot world = World(
                Polygon(3, 0.25f, 0.25f, 3.75f, 3.5f, true),
                Polygon(2, 0.25f, 1.25f, 3.75f, 3.5f - 0.75f * epsilon, true),
                Polygon(1, 0.25f, 2.25f, 3.75f, 3.5f - 1.5f * epsilon, true));
            Assert.That(world.TryGetSupportBelow(new Vector2(2.5f, 3.9f), out NavigationSupport support), Is.True);
            Assert.That(support.Surface.SourceId, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentSupportQueriesKeepNestedBodyChecksIndependent()
        {
            NavigationWorldSnapshot world = World(Polygon(1, 0.25f, 0.25f, 3.75f, 0.75f, true));
            bool[] results = new bool[32];
            Parallel.For(0, results.Length, index =>
            {
                AABB body = AABB.FromLowerCenter(new Vector2(2.5f, 0.76f), new Vector2(0.4f, 0.8f));
                results[index] = world.TryResolveSupport(body, 0.05f, out NavigationSupport support)
                    && support.Surface.SourceId == 1 && support.Position.y == 0.75f;
            });
            Assert.That(results, Has.All.True);
        }

        [Test, Category("Performance")]
        public void ManyShapes_LocalQueryMeasuresSparseBucketLookupAndBitmapClearing()
        {
            const int shapeGridSide = 128;
            const int warmupQueries = 32;
            const int measuredQueries = 2048;
            List<NavigationShapeData> shapes = new(shapeGridSide * shapeGridSide);
            int sourceId = 0;
            for (int y = 0; y < shapeGridSide; y++)
                for (int x = 0; x < shapeGridSide; x++)
                {
                    float minX = 1.25f + x * 2f;
                    float minY = 1.25f + y * 2f;
                    shapes.Add(Polygon(sourceId++, minX, minY, minX + 0.2f, minY + 0.2f));
                }

            NavigationWorldSnapshot world = World(shapes.ToArray(), AABB.FromMinAndSize(0f, 0f, 256f, 256f));
            AABB localBody = AABB.FromMinAndSize(1.55f, 1.3f, 0.4f, 0.4f);
            bool allQueriesClear = true;
            for (int index = 0; index < warmupQueries; index++)
                allQueriesClear &= world.IsBodyClear(localBody, 0f);

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int index = 0; index < measuredQueries; index++)
                allQueriesClear &= world.IsBodyClear(localBody, 0f);
            stopwatch.Stop();

            Assert.That(allQueriesClear, Is.True);
            TestContext.Out.WriteLine(
                $"[NavigationShapeQuerySparse] shapes={shapes.Count}, queriedBuckets=1, " +
                $"queries={measuredQueries}, elapsed={stopwatch.Elapsed.TotalMilliseconds:F3}ms.");
        }

        private static NavigationWorldSnapshot World(params NavigationShapeData[] shapes)
            => World(shapes, AABB.FromMinAndSize(0f, 0f, 4f, 4f));

        private static NavigationWorldSnapshot World(IReadOnlyList<NavigationShapeData> shapes, AABB bounds)
            => NavigationWorldSnapshot.Create(bounds, shapes, Array.Empty<NavigationRegionData>());

        private static NavigationShapeData Polygon(int sourceId, float minX, float minY, float maxX, float maxY, bool hasSupport = false)
            => new(sourceId, 0, NavigationShapeType.Polygon,
                new[] { new Vector2(minX, minY), new Vector2(maxX, minY),
                    new Vector2(maxX, maxY), new Vector2(minX, maxY) },
                0f, NavigationSurfaceKind.Solid, hasSupport);
    }
}
