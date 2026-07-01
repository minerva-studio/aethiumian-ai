#nullable enable
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Aethiumian.AI
{
    /// <summary>
    /// Serializable 128-bit UUID with zero-alloc compare & hash.
    /// Backed by two ulongs; compatible with Guid.
    /// </summary>
    [Serializable]
    public struct UUID :
        IComparable, IComparable<UUID>, IComparable<Guid>,
        IEquatable<UUID>, IEquatable<Guid>, ISerializationCallbackReceiver
    {
        // ---- Static ----
        public static readonly UUID Empty = new UUID(0UL, 0UL);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UUID NewUUID() => new UUID(Guid.NewGuid());

        // ---- Serialized payload (no strings) ----
        [SerializeField] private ulong lo; // bytes [0..7]
        [SerializeField] private ulong hi; // bytes [8..15]

        // ---- Runtime caches (not serialized) ----
        [NonSerialized] private Guid guid;          // lazily materialized
        [NonSerialized] private bool guidCached;
        [NonSerialized] private string? cached;     // lazily materialized


        // ---- Constructors ----
        public UUID(ulong hi, ulong lo)
        {
            this.hi = hi;
            this.lo = lo;
            guid = default;
            guidCached = false;
            cached = null;
        }

        public UUID(Guid value)
        {
            var bytes = value.ToByteArray();      // 16 bytes, little-endian shape used by Guid
            lo = BitConverter.ToUInt64(bytes, 0);  // bytes[0..7]
            hi = BitConverter.ToUInt64(bytes, 8);  // bytes[8..15]
            guid = value;
            guidCached = true;
            cached = null;
        }

        public UUID(string value)
        {
            if (!Guid.TryParse(value, out var g)) g = Guid.Empty;
            var bytes = g.ToByteArray();
            lo = BitConverter.ToUInt64(bytes, 0);
            hi = BitConverter.ToUInt64(bytes, 8);
            guid = g;
            guidCached = true;
            cached = NormalizeString(value, g);
        }

        // ---- Public surface ----

        /// <summary> The canonical string (cached). </summary>
        public string Value => cached ??= Numeric.ToString();

        /// <summary> The Guid view (cached). </summary>
        public Guid Numeric
        {
            get
            {
                if (guidCached) return guid;
                Span<byte> b = stackalloc byte[16];
                BitConverter.GetBytes(lo).CopyTo(b);      // [0..7]
                BitConverter.GetBytes(hi).CopyTo(b[8..]); // [8..15]
                guid = new Guid(b);
                guidCached = true;
                return guid;
            }
        }


        // ---- Comparers / Equality ----
        public int CompareTo(UUID other) => Numeric.CompareTo(other.Numeric);
        public int CompareTo(Guid other) => Numeric.CompareTo(other);
        public int CompareTo(object? obj) => obj switch
        {
            UUID u => CompareTo(u),
            Guid g => CompareTo(g),
            _ => throw new ArgumentException("Must be UUID or Guid", nameof(obj))
        };


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(UUID other) => hi == other.hi && lo == other.lo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Guid other)
        {
            var u = new UUID(other);
            return hi == u.hi && lo == u.lo;
        }

        public readonly override bool Equals(object? obj) =>
            obj is UUID u && Equals(u) ||
            obj is Guid g && Equals(g);

        public readonly override int GetHashCode()
        {
            // Mix 128 bits down; fast and stable
            unchecked
            {
                ulong x = hi * 0x9E3779B97F4A7C15UL ^ lo;
                x ^= x >> 33;
                x *= 0xff51afd7ed558ccdUL;
                x ^= x >> 33;
                return (int)(x ^ (x >> 32));
            }
        }

        public override string ToString() => Value;

        // ---- Operators ----
        public static bool operator ==(UUID a, UUID b) => a.Equals(b);
        public static bool operator !=(UUID a, UUID b) => !a.Equals(b);

        public static implicit operator UUID(Guid g) => new UUID(g);
        public static implicit operator Guid(UUID u) => u.Numeric;
        public static implicit operator string(UUID u) => u.Value;

        // ---- Utilities ----
        public readonly (ulong hi, ulong lo) ToTuple() => new(hi, lo);

        public static bool TryParse(string? s, out UUID uuid)
        {
            if (Guid.TryParse(s, out var g))
            {
                uuid = new UUID(g);
                uuid.cached = g.ToString();
                return true;
            }
            uuid = Empty;
            return false;
        }

        // ---- Serialization hooks ----
        // Nothing to do before serialize; primitives are the source of truth.
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            guid = default;
            guidCached = false;
            cached = null;
        }

        // ---- Private helpers ----
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteParts(Span<byte> dest16, ulong lo, ulong hi)
        {
            // dest16 length must be 16
            var loBytes = BitConverter.GetBytes(lo);
            var hiBytes = BitConverter.GetBytes(hi);
            loBytes.CopyTo(dest16);            // [0..7]
            hiBytes.CopyTo(dest16.Slice(8));   // [8..15]
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string NormalizeString(string original, Guid parsed)
        {
            // If original was a valid Guid canonical form, keep it; otherwise use canonical ToString
            return Guid.TryParse(original, out var g) && g == parsed ? original : parsed.ToString();
        }
    }
}
