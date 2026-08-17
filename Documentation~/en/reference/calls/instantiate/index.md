# `Instantiate`

## Purpose

Instantiate a prefab into scene.

## Key inputs / outputs

- Inputs: `original`, `parentOfObject`, `offsetMode`, `offset`.
- Outputs: instantiated `result`.

## Success / Failure semantics

- `Success` for successful `Instantiate`.
- `Failed` when source prefab is invalid.

## Important limitations

- Parent and offset handling limited to supported enum options.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Instantiate.cs)
