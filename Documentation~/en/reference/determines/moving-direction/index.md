# `MovingDirection`

## Purpose

Output movement direction vector.

## Key inputs / outputs

- Inputs: `usePhysics2D`.
- Outputs: `Vector2` direction.

## Success / Failure semantics

- Uses `Rigidbody2D.linearVelocity`/`velocity` when `usePhysics2D`; otherwise uses transform delta.

## Important limitations

- Rigidbody2D missing leads to null velocity path when `usePhysics2D=true`.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/MovingDirection.cs)
