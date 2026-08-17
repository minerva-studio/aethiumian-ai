# `IsInScreen`

## Purpose

Test whether a world position is inside main camera screen.

## Key inputs / outputs

- Inputs: `position` (`Vector2/Vector3/unityObject`).
- Outputs: bool.

## Success / Failure semantics

- `true` when projected point is within `[0, Screen]`.

## Important limitations

- Uses `Camera.main`; fails if no main camera is available.
- Depends on active camera projection.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsInScreen.cs)
