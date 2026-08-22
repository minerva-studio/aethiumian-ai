using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Editor.Exporting
{
    /// <summary>Base value used by the read-only behaviour-tree DOM.</summary>
    internal abstract class DomValue
    {
    }

    /// <summary>Represents an explicit null value.</summary>
    internal sealed class DomNull : DomValue
    {
        internal static readonly DomNull Instance = new DomNull();

        private DomNull()
        {
        }
    }

    /// <summary>Represents a scalar YAML value.</summary>
    internal sealed class DomScalar : DomValue
    {
        internal DomScalar(object value)
        {
            Value = value;
        }

        internal object Value { get; }
    }

    /// <summary>Represents an ordered YAML mapping.</summary>
    internal sealed class DomMapping : DomValue
    {
        private readonly List<DomProperty> properties = new List<DomProperty>();

        internal IReadOnlyList<DomProperty> Properties => properties;

        internal DomMapping Add(string name, DomValue value)
        {
            properties.Add(new DomProperty(name, value ?? DomNull.Instance));
            return this;
        }
    }

    /// <summary>Represents an ordered YAML sequence.</summary>
    internal sealed class DomSequence : DomValue
    {
        private readonly List<DomValue> items = new List<DomValue>();

        internal IReadOnlyList<DomValue> Items => items;

        internal DomSequence Add(DomValue value)
        {
            items.Add(value ?? DomNull.Instance);
            return this;
        }
    }

    /// <summary>One ordered property in a DOM mapping.</summary>
    internal sealed class DomProperty
    {
        internal DomProperty(string name, DomValue value)
        {
            Name = name;
            Value = value ?? DomNull.Instance;
        }

        internal string Name { get; }
        internal DomValue Value { get; }
    }
}
