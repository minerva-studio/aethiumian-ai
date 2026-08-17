# `Assign`

## Purpose

Assign a source value into a writable destination variable.

## Key inputs / outputs

- Inputs: `destination` (`VariableReference`), `source` (`VariableField`).
- Outputs: writes to destination.

## Success / Failure semantics

- Success when source type is compatible.
- Failure on incompatible destination/source combinations.

## Important limitations

- The destination variable must be writable.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Assign.cs)
