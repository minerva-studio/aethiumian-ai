# `Jump`

## Purpose

Moves the entity toward its configured goal by following planner-provided ballistic jump steps.

## Key inputs / outputs

- Shared movement inputs: `type`, `path`, and the selected target (`tracing`, `destination`, or wander settings), plus `goal`, `distanceMetric`, and `reachDistance` where applicable.
- Jump parameters: `jumpHeight`, `jumpLength`, `jumpInterval`, and `speedModifier` (which scales the interval between launches).
- Outputs: none.

## Success / Failure semantics

- Success when the configured movement goal is reached while the entity is descending or supported by the ground.
- Failure when the target or route is invalid, or a planned jump cannot be executed.

## Important limitations

- Requires the navigation runtime, a `Rigidbody2D`, enabled navigation colliders, and a control target implementing `IMovementSource`.
- The initial route requires ground support. Each jump launches from a compatible support and waits for the configured interval before another launch.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Jump.cs)
