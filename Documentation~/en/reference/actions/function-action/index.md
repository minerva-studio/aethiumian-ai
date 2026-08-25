# `FunctionAction`

## Purpose

Execute a selected reflected function as an action lifecycle node.

## Key inputs / outputs

- Inputs: `function` (`FunctionReference`), `targetObject` (`VariableReference`), `parameters` (`List<Parameter>`), `returnMode` (`ReturnMode`), `result` (`VariableReference`).
- Outputs: `result` receives the method return value when available.

## Success / Failure semantics

- `Default` succeeds after normal completion; `NodeProgress.Complete(bool)` remains an explicit result.
- `ReturnValue` maps `Task<T>` / `Awaitable<T>` results through the variable-to-bool rules. `Task`, `Awaitable`, and `IEnumerator` have no value and use default completion.
- `AlwaysSuccess` and `AlwaysFailure` force normal completion. Early `NodeProgress` completion logs a warning in non-default modes; exceptions and cancellation retain their defined failure/exception behavior.

## Important limitations

- Static calls are supported while instance calls require a valid target.
- The called method must satisfy action-method validation.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/FunctionAction.cs)
