# Aethiumian.AI Graph Editor Roadmap

Last updated: 2026-08-13

This document records the long-term design and implementation roadmap for the
Aethiumian.AI Graph Editor. It belongs to the standalone Aethiumian.AI package,
not to the Library of Meialia project roadmap.

The Graph Editor is intended to become a readable and safe editor for authored
behaviour trees. It must preserve the runtime tree model and remain useful as a
complement to the existing Nodes page until graph editing reaches feature
parity.

Development and validation follow the staged workflow in
[GraphEditorDevelopmentWorkflow.md](GraphEditorDevelopmentWorkflow.md).

## Status Definitions

- **Implemented**: the behavior exists and has corresponding validation.
- **Partial**: part of the design exists, but the behavior or editing experience
  is not yet complete.
- **Planned**: the direction is established, but implementation has not started.
- **Deferred**: the capability is intentionally retained for a later milestone.

An implementation being present does not by itself mean that its visual editing
experience has been accepted.

## Current Baseline

### UI Toolkit shell — Implemented

- The window shell is declared with UXML and USS and loaded through an
  `EditorWindow` default reference rather than an assumed package path.
- The shell provides tree selection, Nodes, Graph, Variables, and Properties
  tabs, selection locking, clipboard actions, settings, and maintenance tools.
- Nodes, Variables, and Properties retain their existing implementations in
  separate `IMGUIContainer` hosts.
- Rebuilding `CreateGUI()` does not duplicate lifecycle registrations or dirty
  the selected `BehaviourTreeData`.

### Native graph canvas foundation — Implemented

- Graph uses a custom UI Toolkit canvas and does not depend on experimental
  GraphView or Graph Toolkit APIs.
- The canvas supports middle-button or Alt-left pan, zoom, single and multi-selection, box
  selection, grouped node layout dragging, Fit All, Frame Selected, and
  explicit Auto Layout.
- A single `IMGUIContainer` reuses the existing node drawer for the selected
  node inspector.
- Selection is shared with the Nodes page.
- The graph supports authored connection/order editing and node lifecycle
  commands while keeping topology ownership in the editor command layer.
- Presentation-only Entrance and Exit boundaries make the runtime Head and
  global completion boundary visible. Entrance connection gestures and Set as
  Head share the same authored Head contract.
- Existing Service-on-Service references remain readable, while every editor
  creation and replacement path rejects new Service-hosted Services.

### Topology and layout ownership — Implemented

- Authoritative topology comes only from `BehaviourTreeData.nodes` and
  `NodeAccessorProvider` reference accessors.
- Legacy graph connections are never interpreted as runtime topology.
- Ordinary child references, Service references, and optional raw references
  remain distinct relation kinds.
- Native graph coordinates use a versioned editor-only UUID-to-position layout.
- Opening, refreshing, selecting, or importing legacy coordinates does not
  create an asset diff.
- Layout is written only by an explicit move or Auto Layout action, with Undo
  and asset dirtying owned by that action.
- Legacy graph node positions remain available as a read-only compatibility
  source until migration is deliberately completed.

## Presentation Redesign — Implemented

The first presentation experiments have improved readability, but they are not
the final visual language.

### Implemented portions

1. `GraphPresentation` separates visual semantics from the authoritative
   topology snapshot.
2. Sequence is currently shown as a free semantic flow using `start` and `next`
   relations instead of permanently trapping its members in a rigid frame.
3. Condition is a compound presentation whose predicate is embedded while its
   True and False branches remain semantic relations.
4. Auto Layout now reads presentation relations and measured compound sizes
   rather than laying out the old raw reference tree again.
5. Sequence order prefers a vertical continuation, Condition reserves its real
   compound bounds, and unreachable nodes wrap instead of expanding into one
   unlimited row.
6. Nested Sequence presentation now exposes a derived completion endpoint and
   lightweight scope rail. An outer Sequence continues from the inner `END`
   marker rather than directly from the inner Sequence card.
7. Composite Flow completion now uses one editor-only scope model. Authored
   references, derived completion and control relations, and placeholder hints
   have separate roles so future topology editing cannot mistake visual
   semantics for a writable `NodeReference`.
8. Condition now keeps its predicate embedded while True and False branches
   remain free. Both branches converge through a non-interactive
   `[END · Condition]` marker, with lightweight brackets indicating scope.
   Empty and unresolved branches use non-persistent fallback cards that expose
   their runtime Success or Failed result.
9. Card sizes, structural gaps, Service lanes, placeholders, and completion
   markers now consume one compact presentation metric set. This reduces empty
   connector length without changing persisted node coordinates or topology.
10. Loop now uses a mode-specific free presentation. While, DoWhile, and For
    expose Body, derived Repeat control, and a single END exit; For uses a
    presentation-only count check, while empty or unresolved condition/body
    occurrences use non-persistent placeholders. Repeat relations are derived
    control and cannot be mistaken for editable references.
11. Each real Service now has one first-placement host and a lightweight amber
    scope around its structural subtree. Authored Service rails remain distinct
    from scope boundaries, missing slots use non-persistent placeholders, and
    additional hosts are marked as shared references. Each Service persists an
    independent `followParent` setting (defaulting to enabled); moving a host or
    Service card moves the affected scope as one undoable layout action.
12. Probability and PseudoProbability now expose free authored candidate lanes
    that converge through one completion marker. Constant weights use
    runtime-equivalent percentages and all-zero uniform fallback, while dynamic
    weights retain their variable names without inventing static percentages.
    Disabled candidates remain visible, and empty or unresolved candidates use
    non-persistent terminal placeholders instead of false completion paths.
13. Decision now keeps ordinary authored branches below its owner while ordering
    alternatives from left to right. Successful alternatives always converge at
    one completion marker; failure-to-next hints appear only while the Decision
    is selected, so runtime priority remains inspectable without permanently
    crowding the graph.
14. Parallel now presents a shared concurrent fork. WaitAll uses a wide
    synchronization join, while WaitAny uses a compact `FIRST COMPLETE` join;
    duplicate stacks, empty lists, and invalid branches reflect their distinct
    runtime semantics without turning the parallel branches into alternatives.
15. ForEach now presents its enumerable check, free Body range, derived `Next
    Item` return, and exhausted completion. Missing enumerable, missing Body,
    and unassigned item output remain explicit non-persistent placeholders.
16. Large-tree presentation now uses a compact vertical rhythm and a Head-first
    initial frame. Detached nodes remain independent cards; Fit All remains the
    explicit way to include every reachable, auxiliary, and unreachable item.

### Remaining visual acceptance work

- The implemented vocabulary still requires manual acceptance against the three
  representative production AI assets before Graph can be considered a full
  replacement for reading the Nodes page.
- The presentation remains read-only and has no editable connection handles.

## Milestone A: Complete Read-Only Semantic Presentation — Implemented, pending manual acceptance

Graph editing must not begin until the canvas can reliably explain the current
tree.

- Define a shared visual vocabulary for entry, child execution, completion,
  return, and auxiliary references.
- Extend the implemented nested Sequence completion vocabulary to the other
  composite flow families.
- Complete the presentation classifiers and layouts for Parallel and ForEach.
  Condition, Decision, Loop, Probability, PseudoProbability, Parallel, and
  ForEach completion are implemented, but their final editing representations
  remain subject to Milestone A visual acceptance.
- Give Probability weights, Condition True/False branches, Decision priority,
  and ordered collection indices stable labels and anchors.
- Service host ownership, stable auxiliary lanes, free subtree scopes, and
  per-Service host following are implemented. Their final visual density and
  routing remain subject to Milestone A visual acceptance.
- Improve deterministic layout, side-rail routing, compact density,
  unreachable placement, and large-tree framing. Routing audit and crossing
  reduction remain a later visual batch.
- Keep topology read-only throughout this milestone.

Acceptance requires representative large trees to be understandable without
consulting the Nodes page for basic execution order.

### Planned completion syntax by Flow family

- **Probability / PseudoProbability — implemented**: one weighted branch is
  selected, every eligible valid occurrence converges into one completion
  marker, and the outer flow continues from that marker. Constant zero weights
  remain visible as disabled candidates when the total is positive; an all-zero
  set uses the runtime uniform fallback. Variable weights remain dynamic rather
  than displaying guessed percentages.
- **Decision — implemented**: authored alternatives remain direct tree branches
  below the Decision and are arranged left to right. Every Success converges at
  one completion marker, while selection-only failure hints explain how runtime
  priority advances to the next alternative. An empty list returns Failed;
  empty or unresolved occurrences remain explicit Error terminals.
- **Parallel — implemented**: a shared fork starts all unique valid branches.
  WaitAll uses a synchronization join, while WaitAny uses a first-complete join;
  empty, duplicate, and invalid occurrences expose their runtime behavior.
- **Loop / While — implemented**: condition enters Body or Exit. Body
  completion follows a visually distinct Repeat relation back to condition,
  while false exits through END.
- **Loop / DoWhile — implemented**: Body executes before condition. True
  follows Repeat back to Body, while false exits through END.
- **Loop / For — implemented**: a presentation-only count check enters Body,
  Body completion follows Repeat, and exhaustion exits through END.
- **ForEach — implemented**: an enumerable check enters Body, every normal Body
  completion follows a derived Next Item return, and exhaustion exits through
  END. Missing enumerable or Body data remains explicit presentation-only
  error/failure state.

Repeat, synchronization, completion, and placeholder relations remain
presentation-only. They must never become editable topology handles merely
because they are visible on the canvas.

## Milestone B: Topology Editing Service — Implemented

Maintain the view-independent command layer used by Graph connection gestures.

- Describe connectable single references, ordered collections, raw references,
  Service slots, Condition branches, and weighted Probability entries through
  `NodeAccessorProvider`-backed metadata.
- Provide Connect, Disconnect, Replace, Insert, Remove, and Reorder commands.
- Keep SerializedProperty writes, UUID preservation, Undo, asset dirtying, and
  topology rebuilding inside the editing owner rather than canvas elements.
- Define deterministic behavior for empty references, missing targets,
  duplicate references, multiple parents, cycles, and invalid targets.
- Verify every mutation through focused EditMode tests before exposing it to
  pointer interaction.

The command layer and its focused EditMode coverage are complete, including
occurrence-addressed ordered Reorder and weighted-entry metadata preservation.

The command layer must never derive real topology from presentation edges or
legacy graph connections.

## Milestone C: Connection and Order Editing — Implemented, pending manual acceptance

- Display editable ports separately from read-only semantic anchors.
- Support drag-to-connect, replace, disconnect, and cancellation.
- Highlight only compatible targets while dragging and render a temporary
  connection preview.
- Provide explicit ordered editing for Sequence, Decision, Probability, and
  Parallel collections.
- Keep Condition predicate, True, and False slots fixed and unambiguous.
- Restrict Service connections to compatible host slots.
- Rebuild the presentation after each completed command and record each user
  gesture as one Undo operation.
- Provide Move First, Move Earlier, Move Later, Move Last, and Disconnect from
  authored collection-edge context menus, with boundary-disabled actions.

Automated command, menu, Undo/Redo, and layout-preservation coverage is
complete; representative production-AI manual acceptance remains pending.

## Milestone D: Node Lifecycle Editing — Implemented

- Add node search and creation.
- Support inserting a new node from a compatible port.
- Delete nodes with explicit handling for every incoming and outgoing
  reference.
- Integrate duplicate and copy/paste with the existing shared AI Editor
  clipboard.
- Add node and canvas context menus without duplicating domain mutation logic in
  visual elements.
- Preserve selection and data equivalence between the Graph and Nodes pages.
- Acceptance covers blank creation, single/list/Service creation, Rename,
  Duplicate, Paste Value, Paste Under/Before/After, deletion impact handling,
  selection, layout preservation, and one-step Undo/Redo for mutations.
- Add Set as Head to authored non-Service Graph node context menus, preserving
  the existing Head contract and keeping current Head/Service/foreign nodes
  disabled.

## Milestone E: Advanced Editing Experience — Implemented, pending manual acceptance

- Graph-owned ordered multi-selection with the Nodes page retaining its
  single-node selection contract.
- Shader Graph-style blank-canvas box selection, middle-button or Alt-left pan, additive
  selection, and one-node Inspector versus multi-selection summary behavior.
- A darker Graph workspace and a canvas-local floating view panel can hide or
  show the navigation grid without writing editor state into the tree asset.
- Graph navigation, layout, Raw-reference, and Inspector commands live in the
  collapsible canvas-local panel so the window retains only one toolbar row.
- Group movement, subgraph copy/paste, duplication, deletion, Frame Selected,
  and one-step Undo/Redo for each completed gesture.
- Multi-node clipboard content preserves internal authored relations and
  relative layout while remaining unavailable to legacy single-root paste
  commands.

Remaining advanced-experience backlog:

- Keyboard navigation beyond the implemented editing shortcuts.
- Alignment, snapping, and layout assistance.
- Large-graph navigation and an optional minimap.
- VisualElement reuse and edge repaint performance work based on measured large
  trees.

Acceptance requires the complete selection, grouped mutation, cross-tree
clipboard, and Undo/Redo workflow to be exercised on the three representative
production AI assets without unintended asset changes.

## Milestone F: Runtime Debugging — Deferred

- Highlight the active execution stack and currently running nodes.
- Display Success, Failed, Running, and interruption state without writing it to
  the asset.
- Show Service activation and timing state.
- Provide Follow Active Node and paused inspection workflows.
- Require an explicit debug target when multiple `BehaviourTree` instances use
  the same data asset.

## Milestone G: Compatibility and Completion — Deferred

- Validate legacy coordinate migration across existing authored AI assets.
- Remove `Graph`, `GraphNode`, `Connection`, and `ConnectionPoint` compatibility
  types only after all required legacy coordinates have been migrated and the
  removal is separately approved.
- Update the English and Chinese user documentation only for behavior that is
  implemented and accepted.
- Complete light/dark theme, domain reload, safe mode, multi-window, large-tree,
  and no-unintended-YAML-diff validation.
- Retain the Nodes page until the Graph page provides proven editing parity.

## Backlog: Global Sequence Display Mode

This is a future presentation option, not the next implementation milestone. It
must not be used as a shortcut around unfinished flow completion semantics.

The currently agreed direction is:

- Add one graph-wide Free/Container Sequence display mode rather than per-node
  overrides.
- Use Free mode by default.
- In Free mode, give each Sequence a presentation-only completion marker and a
  lightweight scope rail. An outer `next` relation must originate from an inner
  Sequence's completion marker, never directly from its entry card.
- In Container mode, render all Sequences recursively as large nested
  containers.
- Allow nodes inside a container to be selected and inspected, but not dragged
  independently. Layout editing remains a Free-mode responsibility.
- Persist the global mode in editor-only graph layout data with Undo and schema
  migration. Do not create a second set of editable coordinates.
- Keep completion markers, container-local positions, derived bounds, and scope
  rails out of serialized data.
- Preserve the single authoritative UUID-to-position layout and the authored
  BehaviourTree topology.

Before this backlog item is scheduled, Milestone A must define completion
semantics for Sequence and other composite flow nodes.

## Persistent Design Decisions

- Use custom UI Toolkit elements for the canvas rather than experimental
  GraphView APIs.
- Use UXML and USS for stable shell structure and styling; use C# for dynamic
  graph elements and interaction.
- Treat topology, presentation, layout, and runtime debug state as separate
  models with separate owners.
- Keep the existing node drawer through one IMGUI inspector bridge until a
  native replacement is explicitly designed.
- Never dirty a tree merely because the editor opened, refreshed, changed
  selection, or imported legacy coordinates in memory.
- Keep raw references opt-in and exclude them from automatic structural layout.
- Preserve duplicate edges and collection order even though one UUID has one
  authoritative node and one persisted free-layout position.

## Roadmap Maintenance

- Add new ideas to the backlog before treating them as the next milestone.
- Move an item to Planned only after dependencies and acceptance criteria are
  understood.
- Update status and validation evidence when a milestone changes.
- Keep unresolved design questions explicit instead of silently choosing an
  implementation during unrelated work.
- Do not describe roadmap items as existing features in `DOC_EN.md` or
  `DOC_ZH.md`.
