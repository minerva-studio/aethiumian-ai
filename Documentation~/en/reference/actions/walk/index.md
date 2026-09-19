# `Walk`

## Purpose

Moves the entity toward its configured goal using planned ground traversal, jumping, falling, or drop-through steps.

## Key inputs / outputs

- Shared movement inputs: `type`, `path`, and the selected target (`tracing`, `destination`, or wander settings), plus `goal`, `distanceMetric`, and `reachDistance` where applicable.
- Walk parameters: `speed`, `speedModifier`, `accelerateRate`, `jumpHeight`, `jumpLength`, and `setFinalPosition` for completed wander behavior.
- Outputs: none.

## Success / Failure semantics

- Success when the configured movement goal is reached.
- Failure when the target or route is invalid, traversal cannot be prepared, or movement cannot continue.

## Important limitations

- Requires the navigation runtime, a `Rigidbody2D`, enabled navigation colliders, and a control target implementing `IMovementSource`.
- Jump segments require a valid grounded launch support and a trajectory accepted by the configured jump parameters.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Walk.cs)
