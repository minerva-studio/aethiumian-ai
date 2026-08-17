# `Loop`

## Purpose

Repeat child branches using fixed count or condition-based loop types.

## Key inputs / outputs

- Inputs: `loopType`, `loopCount` (for count mode), `condition` (for while modes), `events` (`List<TreeNode>`).
- Outputs: none.

## Success / Failure semantics

- Generally succeeds while iterating; can fail when running as service and no child is valid.

## Important limitations

- No events in loop may pause for one frame before continuing.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Loop.cs)
