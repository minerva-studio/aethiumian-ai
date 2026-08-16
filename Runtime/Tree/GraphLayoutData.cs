#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
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
        internal const int CurrentVersion = 4;

        [SerializeField]
        private int version = CurrentVersion;

        [SerializeField]
        private List<GraphLayoutEntry> positions = new();

        [SerializeField]
        private List<GraphServiceLayoutEntry> services = new();

        [SerializeField]
        private List<GraphGroupLayoutEntry> groups = new();

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

        /// <summary>Gets persisted editor-only graph groups.</summary>
        internal IReadOnlyList<GraphGroupLayoutEntry> Groups => groups ??= new List<GraphGroupLayoutEntry>();

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

            RemoveNodes(new HashSet<UUID> { removedUUID });

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

            if (groups != null)
            {
                for (int index = groups.Count - 1; index >= 0; index--)
                {
                    groups[index].RemoveMembers(removedUUIDs);
                    if (groups[index].Members.Count == 0) groups.RemoveAt(index);
                }
            }
        }

        /// <summary>Creates a stable editor-only group and assigns its members.</summary>
        /// <param name="title">The group title.</param><param name="color">The preset color.</param>
        /// <param name="members">Authored node UUIDs.</param>
        /// <returns>The newly created group.</returns>
        internal GraphGroupLayoutEntry AddGroup(string title, Color color, IEnumerable<UUID> members)
        {
            GraphGroupLayoutEntry group = new(UUID.NewUUID(), title, color, members);
            groups ??= new List<GraphGroupLayoutEntry>();
            groups.Add(group);
            return group;
        }

        /// <summary>Removes a group while retaining all authored nodes.</summary>
        /// <param name="groupUUID">The group UUID.</param>
        internal void RemoveGroup(UUID groupUUID) => groups?.RemoveAll(group => group.UUID == groupUUID);

        /// <summary>Replaces one group metadata record without changing topology.</summary>
        /// <param name="group">The replacement group.</param>
        internal void ReplaceGroup(GraphGroupLayoutEntry group)
        {
            groups ??= new List<GraphGroupLayoutEntry>();
            int index = groups.FindIndex(item => item.UUID == group.UUID);
            if (index >= 0) groups[index] = group;
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
            Vector2? exitPosition = null,
            IEnumerable<GraphGroupLayoutEntry> groupEntries = null)
        {
            GraphLayoutData layout = new();
            layout.positions.AddRange(entries);
            if (serviceEntries != null)
            {
                layout.services.AddRange(serviceEntries);
            }
            if (groupEntries != null) layout.groups.AddRange(groupEntries);

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

    /// <summary>Editor-only persisted annotation frame metadata.</summary>
    [Serializable]
    internal struct GraphGroupLayoutEntry
    {
        [SerializeField] private UUID uuid;
        [SerializeField] private string title;
        [SerializeField] private Color color;
        [SerializeField] private List<UUID> members;

        /// <summary>Initializes a graph annotation frame.</summary>
        internal GraphGroupLayoutEntry(UUID uuid, string title, Color color, IEnumerable<UUID> members)
        { this.uuid = uuid; this.title = title; this.color = color; this.members = members?.Distinct().ToList() ?? new List<UUID>(); }
        internal UUID UUID => uuid;
        internal string Title => title;
        internal Color Color => color;
        internal IReadOnlyList<UUID> Members => members ??= new List<UUID>();
        /// <summary>Removes deleted authored members from this frame.</summary>
        internal void RemoveMembers(ISet<UUID> removed) => members?.RemoveAll(removed.Contains);
        /// <summary>Returns a copy with a renamed title.</summary>
        internal GraphGroupLayoutEntry WithTitle(string value) => new(uuid, value, color, Members);
        /// <summary>Returns a copy with a replacement preset color.</summary>
        internal GraphGroupLayoutEntry WithColor(Color value) => new(uuid, title, value, Members);
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
