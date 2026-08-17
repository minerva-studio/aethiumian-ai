# `Sequence`

## Purpose

Execute events in order regardless of each branch return.

## Key inputs / outputs

- Inputs: `events` (`List<TreeNode>`).
- Outputs: none.

## Success / Failure semantics

- Succeeds when at least one child succeeds.

## Important limitations

- Does not stop on child failure.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Sequence.cs)
