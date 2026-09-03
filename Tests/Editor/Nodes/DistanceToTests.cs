using Aethiumian.AI.Editor.Tests.Support;
using Aethiumian.AI.Nodes;
using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aethiumian.AI.Editor.Tests.Distance
{
    /// <summary>Verifies DistanceTo measurement geometry, metrics, and editor constraints.</summary>
    public sealed class DistanceToTests
    {
        [UnityTest]
        public IEnumerator TransformPosition_UsesNonNegativeDistanceMetrics()
        {
            DistanceTo node = TreeTestFixture.CreateNode<DistanceTo>("Distance");
            using TreeTestFixture fixture = TreeTestFixture.Create(node);
            yield return fixture.WaitUntilReady();
            fixture.Start();

            fixture.GameObject.transform.position = Vector2.zero;
            DistanceTo runtime = fixture.GetRuntimeNode(node);
            Vector2 target = new(3f, -4f);
            Assert.That(runtime.Distance(target, DistanceTo.DistanceType.Euclidean), Is.EqualTo(5f).Within(0.0001f));
            Assert.That(runtime.Distance(target, DistanceTo.DistanceType.Manhattan), Is.EqualTo(7f).Within(0.0001f));
            Assert.That(runtime.Distance(target, DistanceTo.DistanceType.Chebyshev), Is.EqualTo(4f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator ColliderBounds_UsesMergedBoundsAndSelectedMetric()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                BoxCollider2D sourceCollider = source.AddComponent<BoxCollider2D>();
                sourceCollider.size = new Vector2(2f, 2f);
                BoxCollider2D targetCollider = target.AddComponent<BoxCollider2D>();
                targetCollider.size = new Vector2(2f, 2f);
                targetCollider.offset = new Vector2(6f, 5f);
                yield return null;

                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(5f).Within(0.0001f));
                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Manhattan), Is.EqualTo(7f).Within(0.0001f));
                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Chebyshev), Is.EqualTo(4f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ColliderSurface_UsesClosestPhysicalDistanceAndIgnoresNonEuclideanMetric()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                BoxCollider2D sourceCollider = source.AddComponent<BoxCollider2D>();
                sourceCollider.size = new Vector2(2f, 2f);
                BoxCollider2D targetCollider = target.AddComponent<BoxCollider2D>();
                targetCollider.size = new Vector2(2f, 2f);
                targetCollider.offset = new Vector2(6f, 5f);
                yield return null;

                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderSurface,
                    DistanceTo.DistanceType.Manhattan), Is.EqualTo(5f).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ColliderMeasurement_RequiresEnabledNonTriggerColliders()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                BoxCollider2D disabled = source.AddComponent<BoxCollider2D>();
                disabled.enabled = false;
                BoxCollider2D trigger = source.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                target.AddComponent<BoxCollider2D>();
                Assert.That(() => DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Euclidean), Throws.TypeOf<MissingComponentException>());
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void NullOrDestroyedTarget_ReturnsInfiniteDistance()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                Assert.That(DistanceTo.Measure(source, null, DistanceTo.Measurement.TransformPosition,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(float.PositiveInfinity));

                Object.DestroyImmediate(target);
                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.TransformPosition,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(float.PositiveInfinity));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ColliderBounds_ReportsZeroForTouchingAndOverlappingBounds()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                source.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                BoxCollider2D targetCollider = target.AddComponent<BoxCollider2D>();
                targetCollider.size = new Vector2(2f, 2f);

                targetCollider.offset = new Vector2(2f, 0f);
                yield return null;
                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(0f).Within(0.0001f));

                targetCollider.offset = new Vector2(1f, 0f);
                yield return null;
                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ColliderBounds_MergesMultipleCollidersAndIgnoresTriggerColliders()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                source.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                GameObject extension = new("DistanceSourceExtension");
                extension.transform.SetParent(source.transform);
                BoxCollider2D extensionCollider = extension.AddComponent<BoxCollider2D>();
                extensionCollider.size = new Vector2(2f, 2f);
                extensionCollider.offset = new Vector2(3f, 0f);
                GameObject trigger = new("DistanceSourceTrigger");
                trigger.transform.SetParent(source.transform);
                BoxCollider2D triggerCollider = trigger.AddComponent<BoxCollider2D>();
                triggerCollider.size = new Vector2(2f, 2f);
                triggerCollider.offset = new Vector2(100f, 0f);
                triggerCollider.isTrigger = true;

                target.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                target.GetComponent<BoxCollider2D>().offset = new Vector2(8f, 0f);
                yield return null;

                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderBounds,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(3f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator ColliderSurface_ReportsZeroForOverlappingColliders()
        {
            GameObject source = new("DistanceSource");
            GameObject target = new("DistanceTarget");
            try
            {
                source.AddComponent<BoxCollider2D>().size = new Vector2(2f, 2f);
                BoxCollider2D targetCollider = target.AddComponent<BoxCollider2D>();
                targetCollider.size = new Vector2(2f, 2f);
                targetCollider.offset = new Vector2(1f, 0f);
                yield return null;

                Assert.That(DistanceTo.Measure(source, target, DistanceTo.Measurement.ColliderSurface,
                    DistanceTo.DistanceType.Euclidean), Is.EqualTo(0f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void ColliderSurface_HidesDistanceMetricInInspector()
        {
            DistanceTo node = new();
            Assert.That(node.measurement, Is.EqualTo(DistanceTo.Measurement.TransformPosition));

            node.measurement = DistanceTo.Measurement.ColliderSurface;
            FieldInfo field = typeof(DistanceTo).GetField(nameof(DistanceTo.distanceType), BindingFlags.Instance | BindingFlags.Public);
            Assert.That(Aethiumian.AI.Editor.NodeDrawerFieldMetadata.ShouldDraw(node, field), Is.False);

            node.measurement = DistanceTo.Measurement.ColliderBounds;
            Assert.That(Aethiumian.AI.Editor.NodeDrawerFieldMetadata.ShouldDraw(node, field), Is.True);
        }
    }
}
