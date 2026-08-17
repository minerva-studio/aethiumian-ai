# `Break`

## Purpose

Break current service flow when condition node succeeds.

## Key inputs / outputs

- Inputs: `returnTo` (`ReturnType`), `condition` (`TreeNode`).
- Outputs: none.

## Success / Failure semantics

- Always reports success when condition branch succeeds and service break is applied.

## Important limitations

- Condition branch returns are evaluated through host service context.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Break.cs)
