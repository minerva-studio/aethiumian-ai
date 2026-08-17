# `CallStatic`

## Purpose

Invoke a static method via reflection.

## Key inputs / outputs

- Inputs: `type`, `methodName`, `parameters`.
- Outputs: optional `result`.

## Success / Failure semantics

- Bool mapping for bool return value.
- `Failed` if static method cannot be resolved.

## Important limitations

- Public static method required with matching parameters.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/CallStatic.cs)
