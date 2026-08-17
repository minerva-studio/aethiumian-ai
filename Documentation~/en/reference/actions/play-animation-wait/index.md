# `PlayAnimationWait`

## Purpose

Play an animator state and wait until the state is no longer current.

## Key inputs / outputs

- Inputs: `stateName` (`VariableField<string>`), `layer` (`VariableField<int>`).
- Outputs: none.

## Success / Failure semantics

- Success when the state hash changes from the started state.
- Failure when Animator component is missing.

## Important limitations

- Update mode handling follows Animator update mode (`AnimatePhysics` vs `Normal`).

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/PlayAnimationWait.cs)
