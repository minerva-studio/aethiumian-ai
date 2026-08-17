# `FunctionCall`

## Purpose

Call a selected function once.

## Key inputs / outputs

- Inputs: `function`, `targetObject`, `parameters`.
- Outputs: optional `result`.

## Success / Failure semantics

- Bool-mapped return for bool methods.
- `Failed` if lookup or invocation throws.

## Important limitations

- Static methods are invoked with null target.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Functions/FunctionCall.cs)
