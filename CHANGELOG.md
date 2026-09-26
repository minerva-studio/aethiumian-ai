# Changelog

All notable changes to Aethiumian.AI are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A native Graph page in the AI Editor, including creation palettes, compatible-port connections, shared clipboard commands, multi-selection, grouped editing, alignment, distribution, and explicit Auto Layout.
- Graph-specific presentation for control-flow branches, loops, parallel flows, probability flows, conditions, services, entrance and exit boundaries, and detached nodes.
- Support for editing the behaviour-tree head and ordered flow children from the Graph page.
- A versioned, editor-only graph-layout payload that preserves authored coordinates across topology changes.
- Persistent per-window editor state, multi-tree editor windows, and shared cross-window node clipboard content.
- Legacy-node migration mappings and editor upgrade paths for supported obsolete nodes.
- `Aggregate` flow and decorator-node support.
- Package documentation, including English and Chinese documentation portals and a generic documentation sample.
- A single coordinate frame per navigation route: `NavigationRoute.CoordinateFrame`, `NavigationRouteSegment.CoordinateFrame`, `NavigationRoute.ResolveBodyAt`/`ResolveEndpointBody`, and the explicit `NavigationRoute.Empty` factory for zero-length routes. A non-empty route derives `Start` and `Endpoint` from its own segment chain, so `Create`/`Complete`/`Partial` no longer ask callers to restate both positions.
- `AABB.FromLowerCenter` for the lower-center body pose that ground-anchored capabilities plan and execute with.

### Changed

- Unity 6 (`6000.x`) is the recommended and CI-tested version. `2021.3` remains the declared minimum and can install the package, but it is no longer tested or guaranteed.
- Unified `FunctionCall` and `FunctionAction` result modes with `Default`, `ReturnValue`, `AlwaysSuccess`, and `AlwaysFailure`; invocation exceptions continue to follow the tree exception rule.
- Reworked variable fields to use the staged payload model, preserving typed constant and tree-variable conversion semantics while reducing boxing in common paths.
- Serialized UUIDs as GUID strings while retaining legacy payload support during migration.
- Reorganized the AI Editor around UI Toolkit chrome with IMGUI-backed Nodes, Variables, and Properties pages.
- Rebuilt Graph node search, creation, selection, topology editing, layout, and visual hierarchy.
- Updated `Sequence` to short-circuit AND semantics; it no longer performs the prior full-execution OR aggregation. Existing assets are not migrated automatically.
- Consolidated navigation positions: `NavigationRoute.Start` and `Endpoint` are derived from the route segments, goals expose their target geometry through `TargetBounds` instead of a universal anchor point, and `NavigationBodyGeometry.GetCenterAnchor` is now `GetBodyCenter`.
- A body pose is now one `AABB` wherever an API needs both a position and a size. `NavigationGoalRequest` completion, guidance, and distance members, the goal world extensions, `NavigationPlanner.Plan`/`PlanSingleStep`, the `MapNavigationRuntime` planning requests, the `Movement` goal and route hooks, and the `INavigationWorld` support and crossing queries all take a body AABB. Planners derive their own Walk/Jump lower-center or Fly center start, `Movement` no longer interprets coordinates on a capability's behalf, and `NavigationRoute.ResolveBodyAt` converts every route position into the body it represents. `NavigationRoute` keeps one source of truth for its own geometry: the segment chain declares `Start`, `Endpoint`, and the coordinate frame. Region membership is one destination-level rule for every movement action: the sampled body's center against the goal target's center, including the Wander candidate pre-check and `FixedJump`, which keeps its lower-center jump geometry. Body profile dimensions and route segment positions remain positional values.

### Fixed

- Preserved graph layouts through topology changes and corrected layout, framing, coordinate-transform, edge-anchor, and node-drawer/graph synchronization issues.
- Guarded graph topology edits against invalid existing-node moves and enforced tree ownership during subtree reuse.
- Corrected dynamic numeric variable-field conversions and nested-condition behavior.
- Corrected graph entrance attachment and interactions, service-hosting constraints, creation-palette navigation, and selection behavior.
- Ground jump planning keeps a flight margin of two Physics2D contact offsets around geometry away from the launch and landing supports, so planned jumps no longer graze obstacle corners or depend on the host project's Physics2D solver iterations.

### Deprecated

- `ComponentAction`, `ComponentCall`, `ObjectAction`, and `ObjectCall` remain available for compatibility and eligible editor upgrade paths; new trees should prefer `FunctionAction` and `FunctionCall`.
- Repeat action calls are deprecated. Use `Loop` with variables, or migrate one-shot action methods to `FunctionAction`.

## Earlier releases

The project did not maintain a changelog before this file was introduced. The following Git tags remain the authoritative record for those releases:

- `v0.1.4`
- `v0.1.3`
- `v0.1.2`
- `v0.1.1`
- `v0.1.0`

[Unreleased]: https://github.com/minerva-studio/aethiumian-ai/compare/v0.1.4...HEAD
