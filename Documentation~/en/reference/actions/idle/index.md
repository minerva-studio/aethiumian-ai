# `Idle`

## Purpose

Let the current object slow down to zero velocity.

## Key inputs / outputs

- Inputs: `time`, `strength` (`VariableField<float>`).
- Outputs: none.

## Success / Failure semantics

- Success when the speed reaches idle state (often immediate without a valid movement body).

## Important limitations

- Motion update is interpolation-based.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Idle.cs)
