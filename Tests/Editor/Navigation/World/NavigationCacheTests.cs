using System.Collections.Generic;
using Aethiumian.AI.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Editor.Tests.Navigation
{
    /// <summary>Verifies bounded cache-owner behavior without exposing cache mechanics through world APIs.</summary>
    public sealed class NavigationCacheTests
    {
        [Test]
        public void SupportCandidateCache_UsesExactKeysAndLruEviction()
        {
            SupportCandidateCache cache = new(2, 8);
            Vector2 body = new(0.8f, 1.5f);
            Rect first = new(0f, 0f, 1f, 1f);
            Rect second = new(1f, 0f, 1f, 1f);
            Rect third = new(2f, 0f, 1f, 1f);

            cache.Publish(first, body, Candidates(1));
            cache.Publish(second, body, Candidates(2));
            Assert.That(cache.TryGet(first, body, out _), Is.True);
            cache.Publish(third, body, Candidates(3));

            Assert.That(cache.TryGet(first, body, out _), Is.True);
            Assert.That(cache.TryGet(second, body, out _), Is.False);
            Assert.That(cache.TryGet(third, body, out _), Is.True);
            Assert.That(cache.TryGet(first, new Vector2(0.81f, 1.5f), out _), Is.False);
        }

        [Test]
        public void SupportCandidateCache_DoesNotRetainDisabledOrOversizedResults()
        {
            Rect bounds = new(0f, 0f, 1f, 1f);
            Vector2 body = new(0.8f, 1.5f);
            SupportCandidateCache disabled = new(0, 0);
            SupportCandidateCache oversized = new(4, 1);

            disabled.Publish(bounds, body, Candidates(1));
            oversized.Publish(bounds, body, Candidates(2, 3));

            Assert.That(disabled.TryGet(bounds, body, out _), Is.False);
            Assert.That(oversized.TryGet(bounds, body, out _), Is.False);
        }

        private static IReadOnlyList<NavigationSupportCandidate> Candidates(params int[] ids)
        {
            NavigationSupportCandidate[] candidates = new NavigationSupportCandidate[ids.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                int id = ids[index];
                candidates[index] = new NavigationSupportCandidate(id,
                    new NavigationSupport(new NavigationSurfaceId(id, 0), NavigationSurfaceKind.Solid,
                        new Vector2(id, 0f), Vector2.up));
            }

            return candidates;
        }

    }
}
