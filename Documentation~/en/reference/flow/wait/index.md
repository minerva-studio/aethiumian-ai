# `Wait`

## Purpose

Wait for configured time or frame count before continuing.

## Key inputs / outputs

- Inputs: `mode` (`realTime` or `frame`), `time` (`VariableField<float>`).
- Outputs: none.

## Success / Failure semantics

- Always success after timeout.

## Important limitations

- Frame mode depends on fixed/update scheduling.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Wait.cs)
