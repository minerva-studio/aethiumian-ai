# `Sequence`

## Purpose

Execute children in order using short-circuit AND semantics.

## Key inputs / outputs

- Inputs: `events` (`NodeReference[]`).
- Output: one success or failure result.

## Success / Failure semantics

- Returns failure immediately when a child fails.
- Returns success when every child succeeds.
- An empty Sequence returns success.

## Important limitations

- This is an intentional behavior change from the previous full-execution OR aggregation. Existing assets now use short-circuit AND semantics without migration.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Sequence.cs)
