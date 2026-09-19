# `Fly`

## Purpose

Moves the entity through planner-provided aerial waypoints toward a target, retreat point, or wander destination.

## Key inputs / outputs

- Shared movement inputs: `type`, `path`, and the selected target (`tracing`, `destination`, or wander settings), plus `goal`, `distanceMetric`, and `reachDistance` where applicable.
- Fly parameters: `speed`, `speedModifier`, `flexibility`, `maxHeight`, and `neverAboveMaxHeight`; `setFinalPosition` controls final placement for a completed wander.
- Outputs: none.

## Success / Failure semantics

- Success when the configured movement goal is reached.
- Failure when the target is invalid, no route can be executed, or the movement action cannot continue.

## Important limitations

- Requires the navigation runtime, a `Rigidbody2D`, enabled navigation colliders, and a control target implementing `IMovementSource`.
- `neverAboveMaxHeight` applies to trace targets. `maxHeight` limits a target relative to support below it; it does not set a global world-space ceiling.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Fly.cs)
