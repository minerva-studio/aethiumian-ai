# `DirectionTo`

## Purpose

Compute direction from source to target.

## Key inputs / outputs

- Inputs: `overrideCenter`, `center`, `target`.
- Outputs: `result` direction vector.

## Success / Failure semantics

- Success when target exists and source context is valid.

## Important limitations

- If `overrideCenter` is true, center must be set.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/DirectionTo.cs)
