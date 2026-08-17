# `TypeOf`

## Purpose

Retrieve runtime .NET type of a variable value.

## Key inputs / outputs

- Inputs: `variable`, `result`.
- Outputs: bool state and `result` type object.

## Success / Failure semantics

- Returns success state as value comparison (`variable != null`).

## Important limitations

- Missing input value throws a node exception.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/TypeOf.cs)
