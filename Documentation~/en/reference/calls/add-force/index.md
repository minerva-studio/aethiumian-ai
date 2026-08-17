# `AddForce`

## Purpose

Apply a 2D force to the current object.

## Key inputs / outputs

- Inputs: `force` (`VariableField<Vector2>`).
- Outputs: none.

## Success / Failure semantics

- `Success` when force is applied.
- `Failed` when no `Rigidbody2D` is available.

## Important limitations

- Requires `Rigidbody2D` on host object.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/AddForce.cs)
