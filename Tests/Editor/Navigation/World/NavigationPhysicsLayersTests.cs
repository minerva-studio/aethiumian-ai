using System.Collections.Generic;
using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Tests.Navigation
{
    /// <summary>Verifies the immutable terrain-query filter exposed by navigation layer configuration.</summary>
    public sealed class NavigationPhysicsLayersTests
    {
        private readonly List<GameObject> objects = new();

        /// <summary>Destroys the isolated query fixtures after each test.</summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index]) Object.DestroyImmediate(objects[index]);
            }

            objects.Clear();
        }

        /// <summary>Includes configured layers while excluding unconfigured layers and triggers.</summary>
        [Test]
        public void TerrainFilter_UsesOnlyConfiguredNonTriggerCandidates()
        {
            Collider2D configured = CreateCollider("configured", NavigationPhysicsTestLayers.DefaultLayer, false);
            Collider2D unconfigured = CreateCollider("unconfigured", NavigationPhysicsTestLayers.DecorationLayer, false);
            Collider2D trigger = CreateCollider("trigger", NavigationPhysicsTestLayers.DefaultLayer, true);
            Physics2D.SyncTransforms();

            ContactFilter2D filter = new NavigationPhysicsLayers(NavigationPhysicsTestLayers.DefaultMask, 0).CreateTerrainFilter();
            Collider2D[] results = new Collider2D[4];
            int count = Physics2D.OverlapCircle(Vector2.zero, 1f, filter, results);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(configured));
            Assert.That(unconfigured, Is.Not.EqualTo(results[0]));
            Assert.That(trigger, Is.Not.EqualTo(results[0]));
        }

        /// <summary>Does not replace an intentional empty configuration with project defaults.</summary>
        [Test]
        public void TerrainFilter_EmptyConfigurationDoesNotFallback()
        {
            ContactFilter2D filter = new NavigationPhysicsLayers(0, 0).CreateTerrainFilter();

            Assert.That(filter.useLayerMask, Is.True);
            Assert.That(filter.layerMask.value, Is.EqualTo(0));
            Assert.That(filter.useTriggers, Is.False);
        }

        /// <summary>Creates independent filter values from the immutable configuration on each call.</summary>
        [Test]
        public void TerrainFilter_ReturnedMutationDoesNotAffectLaterFilters()
        {
            NavigationPhysicsLayers layers = new(
                NavigationPhysicsTestLayers.GeometryMask,
                NavigationPhysicsTestLayers.PlatformMask);
            ContactFilter2D changed = layers.CreateTerrainFilter();
            changed.layerMask = 0;
            changed.useTriggers = true;

            ContactFilter2D later = layers.CreateTerrainFilter();
            Assert.That(later.layerMask.value, Is.EqualTo(NavigationPhysicsTestLayers.TerrainMask));
            Assert.That(later.useTriggers, Is.False);
        }

        /// <summary>Verifies authored movement mode values remain stable when Retreat is appended.</summary>
        [Test]
        public void MovementGoalValuesKeepAuthoredValues()
        {
            Assert.That((int)Aethiumian.AI.Nodes.Movement.Behaviour.Trace, Is.Zero);
            Assert.That((int)Aethiumian.AI.Nodes.Movement.Behaviour.Wander, Is.EqualTo(1));
            Assert.That((int)Aethiumian.AI.Nodes.Movement.Behaviour.FixedDestination, Is.EqualTo(2));
            Assert.That((int)Aethiumian.AI.Nodes.Movement.Behaviour.Retreat, Is.EqualTo(3));
            Assert.That((int)Aethiumian.AI.Nodes.Movement.PathMode.Simple, Is.Zero);
            Assert.That((int)Aethiumian.AI.Nodes.Movement.PathMode.Smart, Is.EqualTo(1));
        }

        /// <summary>Creates a query collider at the origin on the requested layer.</summary>
        private Collider2D CreateCollider(string name, int layer, bool isTrigger)
        {
            GameObject gameObject = new(name) { layer = layer };
            objects.Add(gameObject);
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = isTrigger;
            return collider;
        }
    }
}
