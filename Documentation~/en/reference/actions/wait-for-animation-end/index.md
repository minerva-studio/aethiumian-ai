# `WaitForAnimationEnd`

## Purpose

Wait until a target animation state changes.

## Key inputs / outputs

- Inputs: `animation` (`current` or `stageName`), optional `stageName` (`VariableField<string>`).
- Outputs: none.

## Success / Failure semantics

- Success when tracked animator state hash changes.

## Important limitations

- Requires an Animator on host object.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/WaitForAnimationEnd.cs)
