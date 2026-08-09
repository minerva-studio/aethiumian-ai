#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethiumian.AI
{
    /// <summary>
    /// Editor-only positions for the native AI graph canvas.
    /// </summary>
    [Serializable]
    internal sealed class GraphLayoutData
    {
        /// <summary>
        /// Current serialized layout schema version.
        /// </summary>
        internal const int CurrentVersion = 1;

        [SerializeField]
        private int version = CurrentVersion;

        [SerializeField]
        private List<GraphLayoutEntry> positions = new();

        /// <summary>
        /// Gets the schema version of this layout.
        /// </summary>
        internal int Version => version;

        /// <summary>
        /// Gets the serialized node positions.
        /// </summary>
        internal IReadOnlyList<GraphLayoutEntry> Positions => positions ??= new List<GraphLayoutEntry>();

        /// <summary>
        /// Finds a stored position without creating or modifying layout data.
        /// </summary>
        /// <param name="uuid">The node UUID to look up.</param>
        /// <param name="position">The stored position when present.</param>
        /// <returns>True when a position exists for the UUID.</returns>
        internal bool TryGetPosition(UUID uuid, out Vector2 position)
        {
            if (positions == null)
            {
                position = default;
                return false;
            }

            for (int i = 0; i < positions.Count; i++)
            {
                GraphLayoutEntry entry = positions[i];
                if (entry.UUID == uuid)
                {
                    position = entry.Position;
                    return true;
                }
            }

            position = default;
            return false;
        }

        /// <summary>
        /// Creates a new empty layout with the current schema version.
        /// </summary>
        /// <returns>A new current-version layout.</returns>
        internal static GraphLayoutData Create(IEnumerable<GraphLayoutEntry> entries)
        {
            GraphLayoutData layout = new();
            layout.positions.AddRange(entries);
            return layout;
        }
    }

    /// <summary>
    /// A UUID and its canvas position.
    /// </summary>
    [Serializable]
    internal struct GraphLayoutEntry
    {
        [SerializeField]
        private UUID uuid;

        [SerializeField]
        private Vector2 position;

        /// <summary>
        /// Initializes a layout entry.
        /// </summary>
        /// <param name="uuid">The node UUID.</param>
        /// <param name="position">The canvas position.</param>
        internal GraphLayoutEntry(UUID uuid, Vector2 position)
        {
            this.uuid = uuid;
            this.position = position;
        }

        /// <summary>
        /// Gets the node UUID.
        /// </summary>
        internal UUID UUID => uuid;

        /// <summary>
        /// Gets the canvas position.
        /// </summary>
        internal Vector2 Position => position;
    }
}
#endif
