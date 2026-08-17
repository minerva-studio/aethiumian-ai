# `IsInVision`

## Purpose

Determine whether target is visible from current entity.

## Key inputs / outputs

- Inputs: `target`, `offset`, `maxDistance`, `blockingLayers`.
- Outputs: bool.

## Success / Failure semantics

- `true` when target is within allowed distance and unobstructed by blocking layers.

## Important limitations

- Uses `Collider2D`/`Physics2D.Raycast`.
- Distance fallback relies on collider distance when available.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsInVision.cs)
