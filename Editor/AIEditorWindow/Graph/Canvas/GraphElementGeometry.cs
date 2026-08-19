using Aethiumian.AI.Nodes;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aethiumian.AI.Editor
{
    /// <summary>
    /// Retained geometry contract for canvas elements whose final position derives from
    /// <see cref="GraphPresentationLayout"/>. The canvas refreshes every registered element
    /// without inspecting its concrete type.
    /// </summary>
    internal interface IGraphGeometryElement
    {
        /// <summary>Writes the latest presentation geometry into this retained element.</summary>
        void RefreshGeometry();
    }

    /// <summary>
    /// Retained selection contract for canvas elements that visualize the current authored
    /// selection, boundary selection, or group selection.
    /// </summary>
    internal interface IGraphSelectionElement
    {
        /// <summary>Interprets the current selection snapshot for this one element.</summary>
        void RefreshSelection(GraphSelectionSnapshot selection);
    }

    /// <summary>
    /// Immutable snapshot of the canvas selection state. Elements interpret the data instead
    /// of the canvas enumerating concrete element types.
    /// </summary>
    internal sealed class GraphSelectionSnapshot
    {
        internal GraphSelectionSnapshot(
            HashSet<UUID> selectedUUIDs,
            GraphPresentationKind? boundaryKind,
            UUID groupUUID)
        {
            this.selectedUUIDs = selectedUUIDs ?? new HashSet<UUID>();
            SelectedUUIDs = this.selectedUUIDs;
            BoundaryKind = boundaryKind;
            GroupUUID = groupUUID;
        }

        private readonly HashSet<UUID> selectedUUIDs;

        /// <summary>Gets the authored UUIDs in the current graph selection.</summary>
        internal IReadOnlyCollection<UUID> SelectedUUIDs { get; }

        /// <summary>Gets the currently selected graph boundary kind, if any.</summary>
        internal GraphPresentationKind? BoundaryKind { get; }

        /// <summary>Gets the currently selected annotation group UUID.</summary>
        internal UUID GroupUUID { get; }

        /// <summary>Reports whether one authored UUID participates in the current selection.</summary>
        internal bool Contains(UUID uuid) => selectedUUIDs.Contains(uuid);
    }

    /// <summary>
    /// Shared stateless geometry helpers for retained graph elements.
    /// </summary>
    internal static class GraphElementGeometry
    {
        /// <summary>
        /// Writes one absolute presentation rectangle into a retained element and requests a repaint.
        /// This removes the duplicated rect-writing code spread across scope elements.
        /// </summary>
        /// <param name="element">The retained element to update.</param>
        /// <param name="bounds">The presentation-space rectangle.</param>
        /// <param name="repaint">Whether to repaint after writing.</param>
        internal static void ApplyRect(VisualElement element, Rect bounds, bool repaint = true)
        {
            if (element == null)
            {
                return;
            }

            element.style.left = bounds.x;
            element.style.top = bounds.y;
            element.style.width = bounds.width;
            element.style.height = bounds.height;
            if (repaint)
            {
                element.MarkDirtyRepaint();
            }
        }
    }
}
