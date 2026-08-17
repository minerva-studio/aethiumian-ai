# `PlayAnimation`

## Purpose

Play an animator state immediately.

## Key inputs / outputs

- Inputs: `stateName`, `layer`.
- Outputs: none.

## Success / Failure semantics

- `Success` after play request.
- `Failed` if Animator component is missing.

## Important limitations

- No completion wait logic in this node.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/PlayAnimation.cs)
