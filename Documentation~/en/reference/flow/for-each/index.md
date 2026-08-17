# `ForEach`

## Purpose

Execute a node for each item in an enumerable collection.

## Key inputs / outputs

- Inputs: `enumerable` (`VariableReference`), `item` (`VariableReference`), `event` (`NodeReference`).
- Outputs: none.

## Success / Failure semantics

- Success when iteration completes.

## Important limitations

- `enumerable` must be non-null and implement `IEnumerable`.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/ForEach.cs)
