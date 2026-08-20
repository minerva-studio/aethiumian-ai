# `Repeat`

## Purpose

Execute one child a fixed number of times.

## Key inputs / outputs

- Inputs: `node` (`TreeNode`), `repeatCount` (`VariableField<int>`).
- Outputs: none.

## Success / Failure semantics

- A count of zero or less succeeds without executing the child.
- A missing child fails.
- A failed child fails the Repeat immediately.
- Repeat succeeds after every requested execution succeeds.

## Important limitations

- The count is read once at the start of each Repeat execution.
- Repeat does not provide condition-based termination, Break, or infinite repetition.
- Use `Loop` when the termination condition must change during execution.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Repeat.cs)
