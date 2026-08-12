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
        internal const int CurrentVersion = 3;

        [SerializeField]
        private int version = CurrentVersion;

        [SerializeField]
        private List<GraphLayoutEntry> positions = new();

        [SerializeField]
        private List<GraphServiceLayoutEntry> services = new();

        [SerializeField]
        private bool hasEntrancePosition;

        [SerializeField]
        private Vector2 entrancePosition;

        [SerializeField]
        private bool hasExitPosition;

        [SerializeField]
        private Vector2 exitPosition;

        /// <summary>
        /// Gets the schema version of this layout.
        /// </summary>
        internal int Version => version;

        /// <summary>
        /// Gets the serialized node positions.
        /// </summary>
        internal IReadOnlyList<GraphLayoutEntry> Positions => positions ??= new List<GraphLayoutEntry>();

        /// <summary>
        /// Gets the persisted Service presentation settings.
        /// </summary>
        internal IReadOnlyList<GraphServiceLayoutEntry> Services => services ??= new List<GraphServiceLayoutEntry>();

        /// <summary>Gets whether a persisted Entrance position exists.</summary>
        internal bool HasEntrancePosition => hasEntrancePosition;

        /// <summary>Gets the persisted Entrance position.</summary>
        internal Vector2 EntrancePosition => entrancePosition;

        /// <summary>Gets whether a persisted Exit position exists.</summary>
        internal bool HasExitPosition => hasExitPosition;

        /// <summary>Gets the persisted Exit position.</summary>
        internal Vector2 ExitPosition => exitPosition;

        /// <summary>Removes persisted presentation entries for one deleted node.</summary>
        /// <param name="removedUUID">The UUID that no longer exists in the authored tree.</param>
        internal void RemoveNode(UUID removedUUID)
        {
            if (positions != null)
            {
                for (int index = positions.Count - 1; index >= 0; index--)
                {
                    if (positions[index].UUID == removedUUID)
                    {
                        positions.RemoveAt(index);
                    }
                }
            }

            if (services != null)
            {
                for (int index = services.Count - 1; index >= 0; index--)
                {
                    if (services[index].UUID == removedUUID)
                    {
                        services.RemoveAt(index);
                    }
                }
            }
        }

        /// <summary>
        /// Removes persisted presentation entries for deleted node UUIDs while preserving all other coordinates.
        /// </summary>
        /// <param name="removedUUIDs">The UUIDs that no longer exist in the authored tree.</param>
        internal void RemoveNodes(ISet<UUID> removedUUIDs)
        {
            if (removedUUIDs == null || removedUUIDs.Count == 0)
            {
                return;
            }

            if (positions != null)
            {
                for (int index = positions.Count - 1; index >= 0; index--)
                {
                    if (removedUUIDs.Contains(positions[index].UUID))
                    {
                        positions.RemoveAt(index);
                    }
                }
            }

            if (services != null)
            {
                for (int index = services.Count - 1; index >= 0; index--)
                {
                    if (removedUUIDs.Contains(services[index].UUID))
                    {
                        services.RemoveAt(index);
                    }
                }
            }
        }

        /// <summary>Gets whether this schema version can still supply node coordinates.</summary>
        internal bool HasSupportedPositions => version >= 1 && version <= CurrentVersion;

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

        /// <summary>Gets whether a Service scope follows its first-placement host.</summary>
        /// <param name="uuid">The Service UUID.</param>
        /// <returns>The stored value, or true when old layout data has no setting.</returns>
        internal bool GetServiceFollowParent(UUID uuid)
        {
            if (services != null)
            {
                foreach (GraphServiceLayoutEntry entry in services)
                {
                    if (entry.UUID == uuid)
                    {
                        return entry.FollowParent;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Creates a new empty layout with the current schema version.
        /// </summary>
        /// <returns>A new current-version layout.</returns>
        internal static GraphLayoutData Create(
            IEnumerable<GraphLayoutEntry> entries,
            IEnumerable<GraphServiceLayoutEntry> serviceEntries = null,
            Vector2? entrancePosition = null,
            Vector2? exitPosition = null)
        {
            GraphLayoutData layout = new();
            layout.positions.AddRange(entries);
            if (serviceEntries != null)
            {
                layout.services.AddRange(serviceEntries);
            }

            if (entrancePosition.HasValue)
            {
                layout.hasEntrancePosition = true;
                layout.entrancePosition = entrancePosition.Value;
            }

            if (exitPosition.HasValue)
            {
                layout.hasExitPosition = true;
                layout.exitPosition = exitPosition.Value;
            }
            return layout;
        }
    }

    /// <summary>
    /// Editor-only persisted presentation settings for one Service UUID.
    /// </summary>
    [Serializable]
    internal struct GraphServiceLayoutEntry
    {
        [SerializeField]
        private UUID uuid;

        [SerializeField]
        private bool followParent;

        /// <summary>Initializes one Service presentation setting.</summary>
        internal GraphServiceLayoutEntry(UUID uuid, bool followParent)
        {
            this.uuid = uuid;
            this.followParent = followParent;
        }

        /// <summary>Gets the Service UUID.</summary>
        internal UUID UUID => uuid;

        /// <summary>Gets whether the Service scope follows its first-placement host.</summary>
        internal bool FollowParent => followParent;
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
