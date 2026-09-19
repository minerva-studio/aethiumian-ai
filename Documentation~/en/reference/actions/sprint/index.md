# `Sprint`

## Purpose

Applies a configured force to the AI host for a bounded duration.

## Key inputs / outputs

- Inputs: `duration`, `force`, and `forceApplication` (`Timed` or `Legacy`).
- Outputs: none.

## Success / Failure semantics

- `Timed` succeeds after its force interval completes and fails if the movement source no longer permits movement.
- `Legacy` applies force on each fixed update and ends successfully after the elapsed time exceeds `duration`.

## Important limitations

- Requires a `Rigidbody2D` on the host and a control target implementing `IMovementSource`.
- `Legacy` preserves the older repeated-force behavior; `Timed` uses a bounded force executor.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Sprint.cs)
