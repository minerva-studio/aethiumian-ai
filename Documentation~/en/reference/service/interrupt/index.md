# `Interrupt`

## Purpose

Interrupt the host node when a condition becomes true.

## Key inputs / outputs

- Inputs: `condition` (`TreeNode`), `result` (`ReturnResult`).
- Outputs: none.

## Success / Failure semantics

- Always reports success after interruption request is registered.

## Important limitations

- If condition is boolean type, direct boolean read may bypass branch execution path.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Interrupt.cs)
