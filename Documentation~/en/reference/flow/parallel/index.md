# `Parallel`

## Purpose

Run unique valid child branches concurrently and wait for all branches or the first completed branch.

## Key inputs / outputs

- Inputs: `events` (`NodeReference[]`) and `mode` (`WaitAll` or `WaitAny`).
- Outputs: none.

## Success / Failure semantics

- An empty list succeeds immediately; otherwise the node yields while the selected completion condition is pending.
- Completed branches resolve to success unless one or more branch stacks report exceptions.

## Important limitations

- Missing references and duplicate references are skipped.
- Each scheduled branch uses an independent runtime call stack; stopping Parallel ends those stacks.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Parallel.cs)
