using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies explicit region overlays and the implicit Global region contract.</summary>
    public sealed class NavigationRegionContractTests
    {
        private static readonly AABB WorldBounds = AABB.FromMinAndSize(0f, 0f, 10f, 10f);

        [Test]
        public void UncoveredWorldPositionsShareImplicitGlobalRegion()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                WorldBounds, System.Array.Empty<NavigationShapeData>(), System.Array.Empty<NavigationRegionData>());

            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(9f, 9f)), Is.True);
            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(-0.1f, 1f)), Is.False);
        }

        [Test]
        public void ExplicitRegionsRemainDistinctFromGlobalAndEachOther()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                WorldBounds,
                System.Array.Empty<NavigationShapeData>(),
                new[]
                {
                    new NavigationRegionData(AABB.FromMinAndSize(0f, 0f, 4f, 10f), 0),
                    new NavigationRegionData(AABB.FromMinAndSize(6f, 0f, 4f, 10f), 1),
                });

            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(3f, 9f)), Is.True);
            Assert.That(world.AreInSameRegion(new Vector2(7f, 1f), new Vector2(9f, 9f)), Is.True);
            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(7f, 1f)), Is.False);
            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(5f, 1f)), Is.False);
            Assert.That(world.AreInSameRegion(new Vector2(4.5f, 1f), new Vector2(5.5f, 1f)), Is.True);
        }

        [Test]
        public void ExplicitRegionIdZeroDoesNotCollideWithImplicitGlobal()
        {
            NavigationWorldSnapshot world = NavigationWorldSnapshot.Create(
                WorldBounds,
                System.Array.Empty<NavigationShapeData>(),
                new[] { new NavigationRegionData(AABB.FromMinAndSize(0f, 0f, 4f, 10f), 0) });

            Assert.That(world.AreInSameRegion(new Vector2(1f, 1f), new Vector2(5f, 1f)), Is.False);
            Assert.That(world.AreInSameRegion(new Vector2(5f, 1f), new Vector2(9f, 9f)), Is.True);
        }
    }
}
