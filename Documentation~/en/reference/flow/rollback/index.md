# `Rollback`

## Purpose

Roll back the active node stack to a referenced node.

## Key inputs / outputs

- Inputs: `stopAt` (`RawNodeReference`), `yield` (bool).
- Outputs: none.

## Success / Failure semantics

- Success when target node exists in current stack and stack pointer jumps back.

## Important limitations

- Service-host mode rolls back only the current service stack.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Rollback.cs)
