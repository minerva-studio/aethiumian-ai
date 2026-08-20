# `Capture`

## Purpose

Store a child node's boolean result in a writable variable while forwarding that result unchanged.

## Key inputs / outputs

- Input: `result` (`VariableReference<bool>`).
- Output: none; the captured value is written to `result` when it has a valid reference.

## Success / Failure semantics

- Returns the child result unchanged: success remains success and failure remains failure.
- If `result` has no valid reference, the child result is still forwarded and execution is not otherwise changed.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Decorators/Capture.cs)
