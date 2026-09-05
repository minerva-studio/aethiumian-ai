# `Throttle`

## Purpose

Admit a child only when a shared timer is ready, then start that timer before the child runs.

## Key inputs / outputs

- Inputs: `node` (child), `duration` (`VariableField<float>`), `timer` (`VariableReference<float>`).
- Output: the child result, or failure while the timer is active.

## Success / Failure semantics

- Admission starts the timer before the child executes.
- A failed or interrupted child keeps the consumed timer active.
- An empty child succeeds when admission is allowed.

## Important limitations

- `timer` must resolve to a local `TimerVariable`.
- The duration is read only when a new execution is admitted.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Throttle.cs)
