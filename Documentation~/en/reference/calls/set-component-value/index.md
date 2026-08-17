# `SetComponentValue`

## Purpose

Set component fields/properties on an attached component.

## Key inputs / outputs

- Inputs: `getComponent`, `component`, `type`, setter descriptors.
- Outputs: mutates host/target component.

## Success / Failure semantics

- `Success` when base setter succeeds.

## Important limitations

- `getComponent=false` requires valid object reference.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/SetComponentValue.cs)
