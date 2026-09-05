# `Cooldown`

## Purpose

Admit a child only when a shared timer is ready, then start that timer after the child succeeds.

## Key inputs / outputs

- Inputs: `node` (child), `duration` (`VariableField<float>`), `timer` (`VariableReference<float>`).
- Output: the child result, or failure while the timer is active.

## Success / Failure semantics

- A successful child completion starts the timer and returns success.
- A failed or interrupted child does not consume the timer.
- An empty child is treated as an immediate successful completion when the timer is ready.

## Important limitations

- `timer` must resolve to a local `TimerVariable`.
- The duration is read only when a child succeeds or an empty child is admitted.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Cooldown.cs)
