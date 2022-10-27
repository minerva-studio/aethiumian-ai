using System;
using UnityEngine;

namespace Amlos.AI
{
    [Serializable]
    public struct UUID : IComparable, IComparable<UUID>, IEquatable<UUID>
    {
        [ContextMenuItem("New UUID", "NewUUID")]
        public string value;

        public string Value { get => string.IsNullOrEmpty(value) ? value = Guid.Empty.ToString() : value; set => this.value = value; }

        private UUID(string value)
        {
            this.value = value;
        }

        public static implicit operator UUID(Guid guid)
        {
            return new UUID(guid.ToString());
        }

        public static implicit operator Guid(UUID uuid)
        {
            if (string.IsNullOrEmpty(uuid.Value) || uuid.Value == null) { return Guid.Empty; }
            else
            {
                return new Guid(uuid.Value);
            }
        }

        public bool IsEmpty()
        {
            return Value.Equals(Guid.Empty);
        }

        public int CompareTo(object value)
        {
            if (value == null)
            {
                return 1;
            }

            if (value is not UUID guid)
            {
                throw new ArgumentException("Must be Guid");
            }

            return guid.Value == Value ? 0 : 1;
        }

        public int CompareTo(UUID other)
        {
            return other.Value == Value ? 0 : 1;
        }

        public bool Equals(UUID other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return !string.IsNullOrEmpty(Value) ? new Guid(Value).ToString() : string.Empty;
        }


        public static UUID Empty = Guid.Empty;

        public static UUID NewUUID()
        {
            return Guid.NewGuid();
        }

        public static bool operator ==(UUID u1, UUID u2)
        {
            return u1.Equals(u2);
        }
        public static bool operator !=(UUID u1, UUID u2)
        {
            return !(u1 == u2);
        }

        public static implicit operator string(UUID u)
        {
            return u.Value;
        }
    }
}