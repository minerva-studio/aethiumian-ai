# `Decision`

## Purpose

Run child nodes in order until one returns `true`.

## Key inputs / outputs

- Inputs: `events` (`List<TreeNode>`).
- Outputs: none.

## Success / Failure semantics

- Success when any child returns true.
- Fails when every child returns false.

## Important limitations

- Order of execution follows authoring order.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Decision.cs)
