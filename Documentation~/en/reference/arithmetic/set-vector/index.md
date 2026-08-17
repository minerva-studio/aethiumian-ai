# `SetVector`

## Purpose

Update selected components of an existing vector.

## Key inputs / outputs

- Inputs: `vector`, `setTo`, `x`, `y`, `z`.
- Outputs: modified vector in destination.

## Success / Failure semantics

- Success when selected components are numeric.

## Important limitations

- `setTo` flags control which components to overwrite.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/SetVector.cs)
