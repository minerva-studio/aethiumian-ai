using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Aethiumian.AI.Tests
{
    public class UUIDTest
    {
        [Test]
        public void EquivalentTest()
        {
            Assert.True((UUID)Guid.Empty == UUID.Empty);
            Assert.True(new UUID() == new UUID());
            Assert.True((UUID)new Guid("30683ae4-ac1f-456d-9465-6cb045b165cc") == (UUID)new Guid("30683ae4-ac1f-456d-9465-6cb045b165cc"));
            Assert.True(((UUID)new Guid("30683ae4-ac1f-456d-9465-6cb045b165cc")).Equals((UUID)new Guid("30683ae4-ac1f-456d-9465-6cb045b165cc")));
        }

        // Wrapper to test Unity serialization stability
        [Serializable]
        private class UUIDHolder
        {
            public UUID id;
        }

        private static readonly Guid[] SampleGuids =
            System.Linq.Enumerable.Range(0, 128)
            .Select(i => DeterministicGuid(i))
            .ToArray();

        private static Guid DeterministicGuid(int seed)
        {
            var rng = new System.Random(seed);
            var bytes = new byte[16];
            rng.NextBytes(bytes);
            return new Guid(bytes);
        }

        [Test]
        public void Empty_Is_AllZero_And_Equals_GuidEmpty()
        {
            var u = UUID.Empty;
            Assert.IsTrue(u.Equals(Guid.Empty));
            Assert.AreEqual(Guid.Empty, (Guid)u);
            Assert.AreEqual(Guid.Empty.ToString(), u.ToString());
            Assert.AreEqual(0, u.CompareTo(Guid.Empty));
            Assert.AreEqual(0, u.CompareTo(UUID.Empty));
        }

        [Test]
        public void NewUUID_Is_Not_Empty_And_Is_Unique()
        {
            var set = new HashSet<UUID>();
            for (int i = 0; i < 256; i++)
            {
                var u = UUID.NewUUID();
                Assert.AreNotEqual(UUID.Empty, u);
                Assert.IsTrue(set.Add(u), "Duplicate UUID generated");
            }
        }

        [Test]
        public void Guid_Roundtrip_Preserves_Value()
        {
            foreach (var g in SampleGuids)
            {
                UUID u = g;                 // implicit Guid -> UUID
                Guid back = u;              // implicit UUID -> Guid
                Assert.AreEqual(g, back);
            }
        }

        [Test]
        public void String_Roundtrip_Preserves_Value()
        {
            foreach (var g in SampleGuids)
            {
                var s = g.ToString();
                var u = new UUID(s);
                Assert.AreEqual(s, u.ToString());
                Assert.AreEqual(g, (Guid)u);
            }
        }

        [Test]
        public void Equality_And_HashCode_Consistent()
        {
            var g = SampleGuids[7];
            UUID u1 = g;
            UUID u2 = new UUID(g);
            UUID u3 = UUID.NewUUID();

            Assert.IsTrue(u1.Equals(u2));
            Assert.IsTrue(u1 == u2);
            Assert.IsFalse(u1 != u2);
            Assert.IsFalse(u1.Equals(u3));

            // Equal objects must have equal hash codes
            Assert.AreEqual(u1.GetHashCode(), u2.GetHashCode());

            // Dictionary lookup works
            var dict = new Dictionary<UUID, string> { [u1] = "ok" };
            Assert.AreEqual("ok", dict[u2]);
            Assert.IsFalse(dict.ContainsKey(u3));
        }

        [Test]
        public void CompareTo_Defines_Total_Order_And_Matches_Guid_Order()
        {
            var guids = SampleGuids.ToList();
            var uuids = guids.Select(g => (UUID)g).ToList();

            guids.Sort();                       // Guid's ordering
            uuids.Sort();                       // UUID ordering

            // After sorting, sequences should line up element-wise
            for (int i = 0; i < guids.Count; i++)
            {
                Assert.AreEqual(guids[i], (Guid)uuids[i],
                    $"Ordering diverged at index {i}");
            }
        }

        [Test]
        public void Serialization_ToJson_FromJson_Preserves_Value()
        {
            var holder = new UUIDHolder { id = (UUID)SampleGuids[42] };
            var json = JsonUtility.ToJson(holder);

            var clone = new UUIDHolder();
            JsonUtility.FromJsonOverwrite(json, clone);

            Assert.AreEqual((Guid)holder.id, (Guid)clone.id);
            Assert.AreEqual(holder.id, clone.id);
        }

        [Test]
        public void TryParse_Works_For_Valid_And_Invalid()
        {
            var g = SampleGuids[3];
            Assert.IsTrue(UUID.TryParse(g.ToString(), out var u));
            Assert.AreEqual(g, (Guid)u);

            Assert.IsFalse(UUID.TryParse("not-a-guid", out var bad));
            Assert.AreEqual(UUID.Empty, bad);
        }

        [Test]
        public void ToString_Canonical_Format()
        {
            var g = SampleGuids[91];
            UUID u = g;
            Assert.AreEqual(g.ToString(), u.ToString());
            // Lowercase hex with hyphens; length 36
            Assert.AreEqual(36, u.ToString().Length);
            Assert.IsTrue(u.ToString().Contains("-"));
        }
    }
}
