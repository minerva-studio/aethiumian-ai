# `FixedJump`

## Purpose

Move the current object with a fixed-height parabolic jump toward a target.

## Key inputs / outputs

- Inputs: `jumpHeight` (`VariableField<float>`), `jumpDuration` (`VariableField<float>`), `target` (`VariableField<Vector2|Vector3|UnityObject>`).
- Outputs: none.

## Success / Failure semantics

- Success after jump duration finishes and the node reaches the computed end time.
- Failure when target is empty or invalid.

## Important limitations

- Requires a `Rigidbody2D` on the host.
- Uses fixed-timestep movement.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
