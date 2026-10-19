# `Jump`

## Purpose

Moves the entity toward its configured goal by following planner-provided ballistic jump steps.

## Key inputs / outputs

- Shared movement inputs: `type`, `path`, and the selected target (`tracing`, `destination`, or wander settings), plus `goal`, `distanceMetric`, and `reachDistance` where applicable.
- Jump parameters: `jumpHeight`, `jumpLength`, `jumpInterval`, and `speedModifier` (which scales the interval between launches).
- `allowAirborneLaunch` and `allowAirborneEndpoint` independently permit unsupported launches and endpoints. Both default to false, preserving grounded behavior for existing assets. They use the same solver and executor policies as FixedJump.
- Outputs: none.

## Success / Failure semantics

- Success when the configured movement goal is reached while descending or stationary, with physical ground support unless `allowAirborneEndpoint` is enabled.
- Failure when the target or route is invalid, or a planned jump cannot be executed.

## Important limitations

- Requires the navigation runtime, a `Rigidbody2D`, enabled navigation colliders, and a control target implementing `IMovementSource`.
- Unless `allowAirborneLaunch` is enabled, the initial route waits for physical support and each launch requires compatible navigation support. Enabling it allows an impulse from the current airborne pose without querying launch support.
- With `allowAirborneEndpoint` enabled, the planner can target an airborne goal endpoint; execution still requires an observed endpoint arrival. Supported candidates remain available. Simple selects one improving action at a time and does not guarantee a complete route.
- Each jump waits for the configured launch interval. FixedJump PlannedStep uses the same next-action planning for equivalent inputs but ends after its first successful action.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Jump.cs)
