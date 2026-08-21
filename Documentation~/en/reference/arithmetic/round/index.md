# `Round`

## Purpose

Round a numeric or vector input component-wise to the nearest integer.

## Key inputs / outputs

- Input: `a` (`VariableField`, numeric or vector).
- Output: `result` with each scalar or vector component rounded to an integer value.

## Success / Failure semantics

- Success for supported numeric and vector input.
- Failure for unsupported input types.

## Important limitations

- Integer input is passed through unchanged.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Round.cs)
