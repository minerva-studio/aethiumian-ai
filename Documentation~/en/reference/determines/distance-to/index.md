# `DistanceTo`

## Purpose

Measure distance to a target game object with configurable metric.

## Key inputs / outputs

- Inputs: `distanceType`, `object` (`VariableReference<GameObject>`).
- Outputs: float distance.

## Success / Failure semantics

- Returns `float.PositiveInfinity` when target is missing.
- Otherwise returns metric distance.

## Important limitations

- Available metrics: Euclidean, Manhattan, Chebyshev.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/DistanceTo.cs)
