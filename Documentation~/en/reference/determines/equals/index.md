# `Equals`

## Purpose

Compare two values for equality.

## Key inputs / outputs

- Inputs: `a`, `b`.
- Outputs: bool.

## Success / Failure semantics

- `Success` returns `a == b` according to type rules.
- `Failed` is represented in returned bool behavior via false.

## Important limitations

- Comparable for built-in variable types and Unity object identity checks.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/Equals.cs)
