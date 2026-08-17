# `DestroyComponent`

## Purpose

Remove a component from host game object.

## Key inputs / outputs

- Inputs: `componentReference` (`TypeReference<Component>`).
- Outputs: none.

## Success / Failure semantics

- `Success` after issuing destroy call.

## Important limitations

- Missing target component results in Unity destroy call with null target type.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/DestroyComponent.cs)
