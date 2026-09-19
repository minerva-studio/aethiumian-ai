# `FixedJump`

## Purpose

Performs one ballistic jump toward a configured point or object. It can solve a direct landing or request one planned jump step from navigation.

## Key inputs / outputs

- Inputs: `target` (`VariableField` containing a `Vector2`, `Vector3`, or Unity object), `jumpHeight`, `jumpLength`, and optional `offset`.
- Options: `targetMode` (`Direct` or `PlannedStep`), `targetMeasurement` for planned object targets, `goal`, `distanceMetric`, `reachDistance`, and `skipReached`.
- Outputs: none.

## Success / Failure semantics

- Success when the jump executor completes, or immediately when `skipReached` is enabled and the target already satisfies the configured goal.
- Failure when the target or jump parameters are invalid, navigation cannot provide a usable jump, or execution fails.

## Important limitations

- Requires the navigation runtime, a `Rigidbody2D`, and enabled navigation colliders. Object targets measured from collider bounds need target colliders.
- `PlannedStep` uses the planner's next jump segment; `Direct` solves a ballistic trajectory to the resolved landing point.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
