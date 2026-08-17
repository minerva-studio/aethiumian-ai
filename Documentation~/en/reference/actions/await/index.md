# `Await`

## Purpose

Wait for a referenced async task to complete before returning.

## Key inputs / outputs

- Inputs: `task` (`VariableReference<Task>`).
- Outputs: `result` (`VariableReference`) when a task return value is produced.

## Success / Failure semantics

- Success when the referenced task finishes.
- Failure when the node cannot resolve or execute the async task.

## Important limitations

- Only valid task references are supported.
- If no valid task is provided, runtime cannot produce a meaningful return.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Await.cs)
