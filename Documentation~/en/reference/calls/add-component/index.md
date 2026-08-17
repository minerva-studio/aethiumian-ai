# `AddComponent`

## Purpose

Add a component by type to the selected game object.

## Key inputs / outputs

- Inputs: `component` (`TypeReference<Component>`), `targetGameObject` (`ParentMode` = `underSelf` or `underParent`).
- Outputs: none.

## Success / Failure semantics

- `Success` when component is added.
- `Failed` when target is invalid.

## Important limitations

- `underParent` requires `transform.parent`.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/AddComponent.cs)
