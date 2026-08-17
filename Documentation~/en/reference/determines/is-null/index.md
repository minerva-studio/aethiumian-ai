# `IsNull`

## Purpose

Check whether a variable is null.

## Key inputs / outputs

- Inputs: `variable`.
- Outputs: bool.

## Success / Failure semantics

- `true` when value is Unity null or CLR null.

## Important limitations

- Requires a valid variable reference for deterministic behavior.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsNull.cs)
