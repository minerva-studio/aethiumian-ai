# `FunctionCall`

## Purpose

Call a selected function once.

## Key inputs / outputs

- Inputs: `function`, `targetObject`, `parameters`, `returnMode` (`Default`, `ReturnValue`, `AlwaysSuccess`, or `AlwaysFailure`).
- Outputs: optional `result`.

## Success / Failure semantics

- `Default` succeeds after a normal invocation.
- `ReturnValue` maps the returned value through the project's variable-to-bool conversion rules; unsupported objects use non-null truthiness.
- `AlwaysSuccess` and `AlwaysFailure` force the normal completion state.
- Invocation exceptions follow the tree exception rule; `result` still stores the returned value independently.

## Important limitations

- Static methods are invoked with null target.
- `FunctionCall` does not await `Task`, `Awaitable`, or coroutine returns; use `FunctionAction` when the completed value is needed.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Functions/FunctionCall.cs)
