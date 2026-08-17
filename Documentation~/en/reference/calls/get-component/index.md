# `GetComponent`

## Purpose

Read component instance(s) from self/parent/children.

## Key inputs / outputs

- Inputs: `getMode`, `getMultiple`, `includeInactive`, `type`.
- Outputs: `result` component or component array.

## Success / Failure semantics

- `Success` when requested data exists.
- `Failed` when no result matched.

## Important limitations

- Search scope depends on `getMode` and may include inactive objects.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/GetComponent.cs)
