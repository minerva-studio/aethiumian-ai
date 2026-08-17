# `VectorComponent`

## Purpose

Decompose a vector into component values.

## Key inputs / outputs

- Inputs: `vector`.
- Outputs: `x`, `y`, `z` variables.

## Success / Failure semantics

- Success for vector inputs.

## Important limitations

- Implementation currently writes `x` value to all output slots; validate before use as a source for UI tooling.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/VectorComponent.cs)
