# `Raycast`

## Purpose

Perform one 3D raycast and expose hit result.

## Key inputs / outputs

- Inputs: `center`, `direction`, `distance`, `layerMask`.
- Outputs: `result` (`RaycastHit`).

## Success / Failure semantics

- Returns based on whether collider is hit.

## Important limitations

- Fixed 3D raycast; no hit filtering except layers.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Raycast.cs)
