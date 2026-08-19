# `Aggregate`

## Purpose

Execute every child in order and reduce all child results to one boolean result.

## Key inputs / outputs

- Inputs: `events` (`NodeReference[]`) and `resultMode`.
- Output: one aggregated success or failure result.

## Success / Failure semantics

- `All` returns success only when every child succeeds; an empty Aggregate succeeds.
- `Any` returns success when at least one child succeeds; an empty Aggregate fails.
- `True` runs every child and then returns success; an empty Aggregate succeeds.
- `False` runs every child and then returns failure; an empty Aggregate fails.
- Normal child success or failure never short-circuits execution.
- Errors, exceptions, and interruption are propagated by the behaviour tree and are not converted to ordinary failure.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Aggregate.cs)
