# `Raycast2D`

## Purpose

Perform one 2D raycast and expose hit result.

## Key inputs / outputs

- Inputs: `center`, `direction`, `distance`, `layerMask`.
- Outputs: `result` (`RaycastHit2D`).

## Success / Failure semantics

- Returns based on whether collider is hit.

## Important limitations

- Fixed 2D raycast behavior.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Raycast2D.cs)
