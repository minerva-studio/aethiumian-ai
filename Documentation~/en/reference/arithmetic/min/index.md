# `Min`

## Purpose

Calculate the component-wise minimum of two numeric values or vectors.

## Key inputs / outputs

- Inputs: `a`, `b` (numeric or vector).
- Output: `result` containing the lesser value for each component.

## Success / Failure semantics

- Success for compatible scalar and vector combinations.
- Failure for unsupported combinations.

## Important limitations

- Scalars broadcast to vectors; vector inputs must have matching widths.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/Min.cs)
