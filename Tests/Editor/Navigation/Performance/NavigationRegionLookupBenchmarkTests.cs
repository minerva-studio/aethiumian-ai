using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>
    /// Measures the two storage shapes a captured navigation region can take: the current rasterized
    /// cell-to-region dictionary, and a flat box array searched linearly.
    /// <para>
    /// Three caveats this fixture deliberately encodes. First, the workload has two variants and both are
    /// reported: a hot pair set repeats the same coordinates, which keeps the dictionary's probe slots
    /// cached, while a fresh point per query forces the dictionary to walk its whole table; the scan is
    /// indifferent to the difference. Reporting only one of them would hide the trade-off. Second, the
    /// region data is captured per chunk (16 units) and rasterized at SpatialIndexBucketSize (1 unit), so
    /// the dictionary holds 256 entries per captured box; the footprint test states that relation instead
    /// of asserting a byte budget. Third, footprints are process deltas around an explicit collection, so
    /// they are noisy on the Editor and are not a player memory figure.
    /// </para>
    /// These are Editor/Mono numbers: an upper bound, not a player-build cost. The only production reader
    /// of this query is wander destination validation, so these rows compare storage shapes rather than a
    /// hot path. Runs on demand, never in the normal gate.
    /// </summary>
    public sealed class NavigationRegionLookupBenchmarkTests
    {
        /// <summary>Queries per measured sample, so per-call overhead of the harness is amortized.</summary>
        private const int Batch = 10_000;
        private const int MeasurementCount = 16;
        private const int WarmupCount = 4;

        /// <summary>Chunk size the project's map build captures regions with.</summary>
        private const int ChunkSize = 16;

        private const int RoomCount = 30;
        private const int MapChunksX = 32;
        private const int MapChunksY = 9;

        private static float sink;

        private static readonly AABB WorldBounds = AABB.FromMinAndSize(Vector2.zero,
            new Vector2(MapChunksX * ChunkSize, MapChunksY * ChunkSize));

        private static readonly AABB[] RoomBoxes = CreateRoomBoxes();
        private static readonly int[] RoomIds = CreateRoomIds();
        private static readonly AABB[] ChunkBoxes;
        private static readonly int[] ChunkIds;
        private static readonly NavigationWorldSnapshot Snapshot;

        static NavigationRegionLookupBenchmarkTests()
        {
            ChunkBoxes = CreateChunkBoxes(out int[] chunkIds);
            ChunkIds = chunkIds;
            Snapshot = NavigationWorldSnapshot.Create(WorldBounds, Array.Empty<NavigationShapeData>(),
                BuildCapture(ChunkBoxes, ChunkIds));
        }

        private static readonly Vector2[] HotFirsts = new Vector2[Batch];
        private static readonly Vector2[] HotSeconds = new Vector2[Batch];
        private static readonly Vector2[] FreshFirsts = new Vector2[Batch];
        private static readonly Vector2[] FreshSeconds = new Vector2[Batch];

        /// <summary>Per-query cost of each storage shape, under a hot pair set and under fresh points.</summary>
        [Test, Performance]
        [Explicit("Performance diagnostic: run on demand")]
        public void RegionLookup_StorageShape()
        {
            FillQueryPoints();
            AssertAllShapesAgree();

            RunBatch("Control.EmptyLoop", () =>
                {
                    for (int index = 0; index < Batch; index++) sink += 1f;
                });
            RunBatch("Region.Dictionary.HotPairs", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += Snapshot.AreInSameRegion(HotFirsts[index], HotSeconds[index]) ? 1f : 0f;
                });
            RunBatch("Region.ScanChunkBoxes.HotPairs", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += SameRegionByScan(ChunkBoxes, ChunkIds, HotFirsts[index], HotSeconds[index]) ? 1f : 0f;
                });
            RunBatch("Region.ScanRoomBoxes.HotPairs", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += SameRegionByScan(RoomBoxes, RoomIds, HotFirsts[index], HotSeconds[index]) ? 1f : 0f;
                });
            RunBatch("Region.Dictionary.FreshPoints", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += Snapshot.AreInSameRegion(FreshFirsts[index], FreshSeconds[index]) ? 1f : 0f;
                });
            RunBatch("Region.ScanChunkBoxes.FreshPoints", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += SameRegionByScan(ChunkBoxes, ChunkIds, FreshFirsts[index], FreshSeconds[index]) ? 1f : 0f;
                });
            RunBatch("Region.ScanRoomBoxes.FreshPoints", () =>
                {
                    for (int index = 0; index < Batch; index++)
                        sink += SameRegionByScan(RoomBoxes, RoomIds, FreshFirsts[index], FreshSeconds[index]) ? 1f : 0f;
                });

            LogRows(Batch, "region lookup per query (ns/query, Editor/Mono upper bound; hot pairs keep the dictionary cached)",
                "Control.EmptyLoop",
                "Region.Dictionary.HotPairs",
                "Region.ScanChunkBoxes.HotPairs",
                "Region.ScanRoomBoxes.HotPairs",
                "Region.Dictionary.FreshPoints",
                "Region.ScanChunkBoxes.FreshPoints",
                "Region.ScanRoomBoxes.FreshPoints");
            LogDelta(Batch, "Region.ScanRoomBoxes.FreshPoints", "Region.Dictionary.FreshPoints",
                "fresh-point penalty of the room-box scan over the raster dictionary");
            LogDelta(Batch, "Region.ScanRoomBoxes.HotPairs", "Region.Dictionary.HotPairs",
                "hot-pair penalty of the room-box scan over the raster dictionary");
        }

        /// <summary>
        /// Build footprint of both shapes. The raster dictionary is measured through a real snapshot build,
        /// so the delta covers the whole construction; the box arrays are measured as raw arrays.
        /// </summary>
        [Test, Performance]
        [Explicit("Performance diagnostic: run on demand")]
        public void RegionLookup_Footprint()
        {
            long beforeArrays = GC.GetTotalMemory(true);
            AABB[] chunks = CreateChunkBoxes(out int[] chunkIds);
            AABB[] rooms = CreateRoomBoxes();
            int[] roomIds = CreateRoomIds();
            long afterArrays = GC.GetTotalMemory(true);

            long beforeSnapshot = GC.GetTotalMemory(true);
            NavigationWorldSnapshot snapshot = NavigationWorldSnapshot.Create(WorldBounds,
                Array.Empty<NavigationShapeData>(), BuildCapture(chunks, chunkIds));
            long afterSnapshot = GC.GetTotalMemory(true);

            sink += snapshot.AreInSameRegion(HotFirsts[0], HotSeconds[0]) ? 1f : 0f;

            StringBuilder report = new();
            report.AppendLine("[RegionLookup] footprint over " + MapChunksX + "x" + MapChunksY
                + " chunks (" + WorldBounds.SizeX * WorldBounds.SizeY + " world cells)");
            report.Append("[RegionLookup]   ").Append("rooms=").Append(RoomCount)
                .Append(", captured boxes=").Append(RoomCount * 8)
                .Append(", dictionary entries=").Append(RoomCount * 8 * ChunkSize * ChunkSize)
                .AppendLine();
            report.Append("[RegionLookup]   ").Append("snapshot build delta=")
                .Append(((afterSnapshot - beforeSnapshot) / 1024.0).ToString("F0")).Append(" KB").AppendLine();
            report.Append("[RegionLookup]   ").Append("box arrays delta=")
                .Append(((afterArrays - beforeArrays) / 1024.0).ToString("F1")).Append(" KB")
                .Append(" (").Append(chunks.Length).Append(" chunk boxes + ").Append(rooms.Length).Append(" room boxes)")
                .AppendLine();
            report.Append("[RegionLookup] sink=").Append(sink.ToString("F1"));
            Debug.Log(report.ToString());
        }

        private static void FillQueryPoints()
        {
            for (int index = 0; index < Batch; index++)
            {
                AABB room = RoomBoxes[index % RoomCount];
                HotFirsts[index] = new Vector2(room.MinX + 0.5f, room.MinY + 0.5f);
                HotSeconds[index] = new Vector2(room.MaxX - 0.5f, room.MaxY - 0.5f);
            }

            System.Random random = new(12345);
            for (int index = 0; index < Batch; index++)
            {
                FreshFirsts[index] = RandomPointIn(RoomBoxes[random.Next(RoomCount)], random);
                FreshSeconds[index] = RandomPointIn(RoomBoxes[random.Next(RoomCount)], random);
            }
        }

        /// <summary>All three shapes must answer the same before any timing claim is made.</summary>
        private static void AssertAllShapesAgree()
        {
            for (int index = 0; index < Batch; index += 97)
            {
                bool expected = Snapshot.AreInSameRegion(HotFirsts[index], HotSeconds[index]);
                Assert.That(SameRegionByScan(ChunkBoxes, ChunkIds, HotFirsts[index], HotSeconds[index]),
                    Is.EqualTo(expected), "chunk-box scan disagrees at " + index);
                Assert.That(SameRegionByScan(RoomBoxes, RoomIds, HotFirsts[index], HotSeconds[index]),
                    Is.EqualTo(expected), "room-box scan disagrees at " + index);
            }
        }

        private static Vector2 RandomPointIn(AABB box, System.Random random) => new(
            Mathf.Lerp(box.MinX + 0.5f, box.MaxX - 0.5f, (float)random.NextDouble()),
            Mathf.Lerp(box.MinY + 0.5f, box.MaxY - 0.5f, (float)random.NextDouble()));

        /// <summary>Packs the requested rooms as 4x2 chunk boxes, the shape most rooms actually have.</summary>
        private static AABB[] CreateRoomBoxes()
        {
            AABB[] boxes = new AABB[RoomCount];
            for (int room = 0; room < RoomCount; room++)
                boxes[room] = AABB.FromMinAndSize(
                    new Vector2(room % 8 * 4 * ChunkSize, room / 8 * 2 * ChunkSize),
                    new Vector2(4 * ChunkSize, 2 * ChunkSize));
            return boxes;
        }

        private static int[] CreateRoomIds()
        {
            int[] ids = new int[RoomCount];
            for (int room = 0; room < RoomCount; room++) ids[room] = room;
            return ids;
        }

        /// <summary>Splits every room into the per-chunk boxes the project's capture currently produces.</summary>
        private static AABB[] CreateChunkBoxes(out int[] ids)
        {
            List<AABB> boxes = new();
            List<int> collected = new();
            AABB[] rooms = CreateRoomBoxes();
            for (int room = 0; room < rooms.Length; room++)
                for (float x = rooms[room].MinX; x < rooms[room].MaxX; x += ChunkSize)
                    for (float y = rooms[room].MinY; y < rooms[room].MaxY; y += ChunkSize)
                    {
                        boxes.Add(AABB.FromMinAndSize(new Vector2(x, y), new Vector2(ChunkSize, ChunkSize)));
                        collected.Add(room);
                    }

            ids = collected.ToArray();
            return boxes.ToArray();
        }

        private static List<NavigationRegionData> BuildCapture(AABB[] chunks, int[] ids)
        {
            List<NavigationRegionData> capture = new(chunks.Length);
            for (int index = 0; index < chunks.Length; index++)
                capture.Add(new NavigationRegionData(chunks[index], ids[index]));
            return capture;
        }

        private static void RunBatch(string sampleGroup, System.Action action)
        {
            Measure.Method(action)
                .SampleGroup(sampleGroup)
                .WarmupCount(WarmupCount).MeasurementCount(MeasurementCount).IterationsPerMeasurement(1).Run();
        }

        private static void LogRows(int operationsPerSample, string header, params string[] names)
        {
            StringBuilder report = new StringBuilder();
            report.Append("[RegionLookup] ").AppendLine(header);
            foreach (string name in names)
            {
                double? median = Median(name);
                report.Append("[RegionLookup]   ").Append(name.PadRight(38))
                    .Append(median.HasValue
                        ? (median.Value * 1e6 / operationsPerSample).ToString("F3") + " ns/unit"
                        : "n/a")
                    .AppendLine();
            }

            report.Append("[RegionLookup] sink=").Append(sink.ToString("F1"));
            Debug.Log(report.ToString());
        }

        private static void LogDelta(int operationsPerSample, string baseline, string reference, string label)
        {
            double? baselineMedian = Median(baseline);
            double? referenceMedian = Median(reference);
            if (!baselineMedian.HasValue || !referenceMedian.HasValue) return;
            Debug.Log("[RegionLookup] " + label + " = "
                + ((baselineMedian.Value - referenceMedian.Value) * 1e6 / operationsPerSample).ToString("F3")
                + " ns/unit");
        }

        /// <summary>Reads the recorded samples directly; the framework fills Median/Average only at teardown.</summary>
        private static double? Median(string name)
        {
            IReadOnlyList<SampleGroup> groups = PerformanceTest.Active?.SampleGroups;
            if (groups == null) return null;
            for (int index = 0; index < groups.Count; index++)
            {
                SampleGroup group = groups[index];
                if (group == null || group.Name != name || group.Samples == null || group.Samples.Count == 0)
                    continue;
                List<double> sorted = new(group.Samples);
                sorted.Sort();
                int middle = sorted.Count / 2;
                return sorted.Count % 2 == 1
                    ? sorted[middle]
                    : (sorted[middle - 1] + sorted[middle]) * 0.5;
            }
            return null;
        }

        private static bool SameRegionByScan(AABB[] boxes, int[] ids, Vector2 first, Vector2 second)
            => TryScan(boxes, ids, first, out int firstId)
                && TryScan(boxes, ids, second, out int secondId)
                && firstId == secondId;

        private static bool TryScan(AABB[] boxes, int[] ids, Vector2 point, out int id)
        {
            for (int index = 0; index < boxes.Length; index++)
                if (boxes[index].Contains(point))
                {
                    id = ids[index];
                    return true;
                }

            id = 0;
            return false;
        }
    }
}
