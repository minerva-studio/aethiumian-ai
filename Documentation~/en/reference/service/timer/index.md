# `Timer`

## Purpose

Decrement and publish timer/variable value each service tick.

## Key inputs / outputs

- Inputs: `updatingVariable` (`VariableReference<float>`), `timing` (`Timing`).
- Outputs: none.

## Success / Failure semantics

- Reports success while alive as a service node.

## Important limitations

- Timing mode affects update frequency with fixed updates.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timer.cs)
