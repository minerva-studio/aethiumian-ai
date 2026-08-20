# `ResultChanged`

## Purpose

Monitor a child node and succeed when child result changes between executions.

## Key inputs / outputs

- Inputs: `node` (`NodeReference`).
- Outputs: none.

## Success / Failure semantics

- Success on result transitions; fail when no child or repeated same result.

## Important limitations

- First child return only initializes comparison state and does not count as change.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Decorators/ResultChanged.cs)
