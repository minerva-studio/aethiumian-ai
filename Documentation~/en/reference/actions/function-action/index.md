# `FunctionAction`

## Purpose

Execute a selected reflected function as an action lifecycle node.

## Key inputs / outputs

- Inputs: `function` (`FunctionReference`), `targetObject` (`VariableReference`), `parameters` (`List<Parameter>`), `result` (`VariableReference`).
- Outputs: `result` receives the method return value when available.

## Success / Failure semantics

- Success follows the returned value: bool `true` succeeds, bool `false` fails.
- Task/IEnumerator/Awaitable methods are waited to completion.

## Important limitations

- Static calls are supported while instance calls require a valid target.
- The called method must satisfy action-method validation.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/FunctionAction.cs)
