using System;
using NUnit.Framework;
using UnityEngine;

namespace Aethiumian.AI.Navigation.Tests
{
    /// <summary>Verifies the shared search heap and its key ordering stay deterministic.</summary>
    public sealed class NavigationSearchOrderingTests
    {
        private static NavigationNodeIdentity Lattice(int x, int y, int labelId = 0)
            => NavigationNodeIdentity.Fly(new Vector2Int(x, y), labelId);

        /// <summary>Verifies identical keys compare equal and differing fields never do.</summary>
        [Test]
        public void IdentityOrderingMatchesEquality()
        {
            NavigationNodeIdentity baseline = Lattice(3, 4, 2);

            Assert.That(baseline.CompareTo(Lattice(3, 4, 2)), Is.Zero);
            Assert.That(baseline.Equals(Lattice(3, 4, 2)), Is.True);
            Assert.That(baseline.CompareTo(Lattice(3, 4, 3)), Is.Not.Zero);
            Assert.That(baseline.CompareTo(Lattice(3, 5, 2)), Is.Not.Zero);
            Assert.That(baseline.CompareTo(Lattice(4, 4, 2)), Is.Not.Zero);
            Assert.That(baseline.CompareTo(NavigationNodeIdentity.Ground(2)), Is.Not.Zero);
        }

        /// <summary>Verifies the ordering is total: antisymmetric with a transitive sample.</summary>
        [Test]
        public void IdentityOrderingIsTotal()
        {
            NavigationNodeIdentity first = Lattice(0, 0);
            NavigationNodeIdentity second = Lattice(0, 0, 1);
            NavigationNodeIdentity third = Lattice(0, 1);

            Assert.That(Math.Sign(first.CompareTo(second)), Is.EqualTo(-Math.Sign(second.CompareTo(first))));
            Assert.That(first.CompareTo(second), Is.LessThan(0));
            Assert.That(second.CompareTo(third), Is.LessThan(0));
            Assert.That(first.CompareTo(third), Is.LessThan(0));
            Assert.That(third.CompareTo(Lattice(9, 0)), Is.GreaterThan(0),
                "The lattice compares y before x, so a lower row sorts first whatever its column is.");
        }

        /// <summary>Verifies a lower score always wins, whatever the keys are.</summary>
        [Test]
        public void HeapPrefersLowerScoreRegardlessOfKey()
        {
            NavigationMinHeap<NavigationNodeIdentity> heap = new();
            NavigationNodeIdentity expensive = Lattice(1, 1);
            NavigationNodeIdentity cheap = Lattice(5, 5);

            heap.Enqueue(expensive, 2f, 0f);
            heap.Enqueue(cheap, 0.5f, 0f);

            Assert.That(heap.Dequeue(out float score), Is.EqualTo(cheap));
            Assert.That(score, Is.EqualTo(0.5f));
            Assert.That(heap.Dequeue(out _), Is.EqualTo(expensive));
            Assert.That(heap.IsEmpty, Is.True);
        }

        /// <summary>Verifies the heuristic decides once scores agree within the epsilon band.</summary>
        [Test]
        public void HeapUsesHeuristicWhenScoresTieWithinEpsilon()
        {
            NavigationMinHeap<NavigationNodeIdentity> heap = new();
            NavigationNodeIdentity later = Lattice(5, 5);
            NavigationNodeIdentity closer = Lattice(1, 1);

            heap.Enqueue(later, 1f, 0.9f);
            heap.Enqueue(closer, 1f + NavigationConstant.Epsilon * 0.5f, 0.1f);

            Assert.That(heap.Dequeue(out _), Is.EqualTo(closer));
        }

        /// <summary>Verifies the key decides once score and heuristic both agree within the band.</summary>
        [Test]
        public void HeapFallsBackToKeyWhenScoreAndHeuristicTie()
        {
            NavigationMinHeap<NavigationNodeIdentity> heap = new();
            NavigationNodeIdentity later = Lattice(5, 5);
            NavigationNodeIdentity closer = Lattice(1, 1);

            heap.Enqueue(later, 1f, 0.3f);
            heap.Enqueue(closer, 1f - NavigationConstant.Epsilon * 0.5f, 0.3f + NavigationConstant.Epsilon * 0.5f);

            Assert.That(heap.Dequeue(out _), Is.EqualTo(closer));
        }

        /// <summary>Verifies one lattice cell can hold several labels and orders them by label.</summary>
        [Test]
        public void HeapRetainsEachLabelOfOneLatticeCellAndOrdersByLabel()
        {
            NavigationMinHeap<NavigationNodeIdentity> heap = new();
            NavigationNodeIdentity second = Lattice(3, 3, 1);
            NavigationNodeIdentity first = Lattice(3, 3, 0);

            heap.Enqueue(second, 1f, 0.2f);
            heap.Enqueue(first, 1f, 0.2f);

            Assert.That(heap.Dequeue(out _), Is.EqualTo(first));
            Assert.That(heap.IsEmpty, Is.False, "Both labels of the same cell must be retained.");
            Assert.That(heap.Dequeue(out _), Is.EqualTo(second));
            Assert.That(heap.IsEmpty, Is.True);
        }

        /// <summary>Verifies an empty open set fails loudly instead of returning a default key.</summary>
        [Test]
        public void HeapRejectsDequeueWhenEmpty()
        {
            NavigationMinHeap<NavigationNodeIdentity> heap = new();

            Assert.That(heap.IsEmpty, Is.True);
            Assert.That(heap.TryPeekScore(out _), Is.False);
            Assert.Throws<InvalidOperationException>(() => heap.Dequeue(out _));
        }
    }
}
