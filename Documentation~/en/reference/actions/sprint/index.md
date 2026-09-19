# `Sprint`

## Purpose

Applies force to the AI host during a bounded movement-state window and reports `Sprinting` / `Idle` through `IMovementSource`.

## Inputs

- `force`: a bindable `Vector2` force value.
- `duration`: the movement-state window; in `Repeated` mode it also limits the force-application window.
- `forceApplication`: `Once` or `Repeated`, defaulting to `Once`.

## Force application

- `Once`: applies `ForceMode2D.Force` once when the action starts, then remains active until `duration` elapses so the whole window reports as `Sprinting`.
- `Repeated`: applies `ForceMode2D.Force` on each eligible fixed update until `duration` elapses.
- With zero duration, `Once` still applies one force but does not report a zero-length `Sprinting` window; `Repeated` applies no force.

## Lifecycle

- If the movement source cannot move when the action starts, the action fails without applying force.
- If movement becomes unavailable while running, force application stops, the action reports `Idle`, and fails.
- Successful completion and action destruction report `Idle`.
- The host must have a `Rigidbody2D`, and the control target must implement `IMovementSource`. Force must be finite and duration must be finite and non-negative.
