# `FixedJump`

## Purpose

Performs one ballistic jump toward a configured point or object. It can solve a direct landing or request one planned jump step from navigation.

## Key inputs / outputs

- Inputs: `target` (`VariableField` containing a `Vector2`, `Vector3`, or Unity object), `jumpHeight`, `jumpLength`, and optional `offset`.
- Options: `targetMode` (`Direct` or `PlannedStep`), `targetMeasurement` for planned object targets, `goal`, `distanceMetric`, `reachDistance`, and `skipReached`.
- Independent options: `allowAirborneLaunch` and `allowAirborneEndpoint`, both false by default, including for existing assets without these fields. Enabling them permits an airborne impulse and an airborne endpoint respectively; endpoint completion still requires observing the descending body reach that endpoint.
- Outputs: none.

## Success / Failure semantics

- Success when the jump executor completes, or immediately when `skipReached` is enabled and the target already satisfies the configured goal.
- Failure when the target or jump parameters are invalid, navigation cannot provide a usable jump, or execution fails.

## Important limitations

- Requires the navigation runtime, a published world, a `Rigidbody2D`, and enabled navigation colliders even when both airborne options are enabled. Direct object targets need target colliders; PlannedStep falls back to the transform position when no target colliders exist, matching Jump Trace.
- `PlannedStep` uses the same single-step planner as `Jump` with `path = Simple`. Equivalent body, target geometry, goal, support policies, and physical inputs select the same step; FixedJump finishes after that step, while Jump continues toward its goal. The default collider measurement and zero offset match Jump Trace; explicit transform measurement or offset intentionally changes the goal geometry.
- `Direct` validates a trajectory to the supplied lower-center endpoint without searching candidate landings. Both modes retain world/region checks, collision validation, jump limits, and one-way platform handling.
- With `allowAirborneEndpoint` enabled, planning also tries the target's lower center as an airborne goal endpoint. Existing supported candidates remain available; this does not add a free-flight search lattice or guarantee a route to every airborne goal.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
