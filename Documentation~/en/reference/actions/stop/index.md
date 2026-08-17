# `Stop`

## Purpose

Decelerate or stop movement with selectable strategies.

## Key inputs / outputs

- Inputs: `idleType`, `speed`, `velocityErrorBound`, `time`.
- Outputs: none.

## Success / Failure semantics

- Success when movement reaches stop state.

## Important limitations

- Requires `Rigidbody2D`.
- Missing body results in immediate completion.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Stop.cs)
