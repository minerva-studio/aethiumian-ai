# `WaitForAnimationEnd`

## Purpose

Wait until an Animator state finishes or is interrupted.

## Key inputs / outputs

- Inputs: `animation` (`current` or `stageName`), optional `stageName` (`VariableField<string>`).
- In `stageName` mode, `stageName` must be a full Layer 0 state path, such as `Base Layer.Attack` or `Base Layer.Combat.Attack`.
- Outputs: none.

## Success / Failure semantics

- In `current` mode, tracks the Layer 0 state active when the action starts. In `stageName` mode, waits for the configured state to become current before tracking it.
- Succeeds when the tracked state naturally finishes (non-looping state with normalized time at least `1`), Layer 0 begins a transition, or the current state changes.
- Looping states do not finish naturally; they remain active until interrupted by a transition or state change. Self-transitions also count as interruptions.
- Fails immediately when the host has no Animator or Runtime Animator Controller, Layer 0 has no valid current state in `current` mode, or the configured full path is empty or does not identify a Layer 0 state.

## Important limitations

- Requires an Animator with a Runtime Animator Controller on the host object.
- Samples `Fixed` Animator update mode in `FixedUpdate`; `Normal` and `UnscaledTime` modes in `Update`.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/WaitForAnimationEnd.cs)
