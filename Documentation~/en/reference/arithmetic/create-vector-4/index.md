# `CreateVector4`

## Purpose

Build a `Vector4` from numeric or vector components.

## Key inputs / outputs

- Inputs: `x`, `y`, `z`, `w` and their lane selectors.
- Output: `vector` (`VariableReference<Vector4>`).

## Success / Failure semantics

- Succeeds when the output binding is a `Vector4` reference and all lanes can be read and written.
- Fails when the output binding or any input lane is invalid.

## Important limitations

- Input lanes are resolved through the authored `VectorLane` selections.
- Conversion exceptions are reported through the node's standard arithmetic error handling.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/CreateVector4.cs)
