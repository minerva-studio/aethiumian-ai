# `RaycastDistance`

## Purpose

Measure distance to first raycast hit.

## Key inputs / outputs

- Inputs: `physicsMode`, `center`, `direction`, `distance`, `layerMask`.
- Outputs: float distance (`float.MaxValue` if no hit).

## Success / Failure semantics

- Returns finite distance when hit exists, `float.MaxValue` otherwise.

## Important limitations

- Supports both `Physics2D` and `Physics3D`.
- Invalid position values can fail node validation.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/RaycastDistance.cs)
