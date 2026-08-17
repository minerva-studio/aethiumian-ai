# `CallGameObject`

## Purpose

Invoke a method on a `GameObject` or component by reflection.

## Key inputs / outputs

- Inputs: `getGameObject`, `pointingGameObject`, `methodName`, `parameters`.
- Outputs: optional `result`.

## Success / Failure semantics

- Returns bool-mapped state for bool methods; non-bool methods usually return success.
- `Failed` when method cannot be resolved or invoked.

## Important limitations

- Method signature must match input `parameters`.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/CallGameObject.cs)
