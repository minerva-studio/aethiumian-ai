# `ObjectCall`

## Purpose

Call a method on a provided object through reflection.

## Key inputs / outputs

- Inputs: `object`, `type`, `parameters`.
- Outputs: optional `result`.

## Success / Failure semantics

- Bool-mapped result for bool methods, otherwise success.
- `Failed` on method resolution/invocation error.

## Important limitations

- Requires reflected instance method exists on target type.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/ObjectCall.cs)
