# `Ceil`

## Purpose

Apply ceiling to a numeric or vector input component-wise.

## Key inputs / outputs

- Input: `a` (`VariableField`, numeric or vector).
- Output: `result` with each scalar or vector component rounded upward.

## Success / Failure semantics

- Success for supported numeric and vector input.
- Failure for unsupported input types.

## Important limitations

- Integer input is passed through unchanged.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Ceil.cs)
