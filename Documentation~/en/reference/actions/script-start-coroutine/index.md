# `ScriptStartCoroutine`

## Purpose

Start a coroutine method on the AI script.

## Key inputs / outputs

- Inputs: `methodName`, `afterExecuteAction`.
- Outputs: none.

## Success / Failure semantics

- Success immediately for `continue`, or at coroutine completion for `waitUntilEnd`.

## Important limitations

- Target method must be a valid coroutine entry.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/ScriptStartCoroutine.cs)
