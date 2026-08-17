# `CreateVector3`

## Purpose

Build a `Vector3` from numeric components.

## Key inputs / outputs

- Inputs: `x`, `y`, `z`.
- Outputs: `vector` (`VariableReference<Vector3>`).

## Success / Failure semantics

- Intended success when output vector reference is valid.

## Important limitations

- Runtime currently returns failed state in the shipped source after writing value; verify intent in code.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/CreateVector3.cs)
