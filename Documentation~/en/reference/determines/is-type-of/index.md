# `IsTypeOf`

## Purpose

Check whether a value has an exact runtime type.

## Key inputs / outputs

- Inputs: `variable`, `type`.
- Outputs: bool.

## Success / Failure semantics

- `true` when `variable.Value.GetType() == type`.

## Important limitations

- Requires `type` reference and non-null input variable.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsTypeOf.cs)
