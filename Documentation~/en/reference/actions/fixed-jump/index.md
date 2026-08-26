# `FixedJump`

## Purpose

Move the current object with a fixed-height parabolic jump toward a target.

## Key inputs / outputs

- Inputs: `jumpHeight` (`VariableField<float>`), `speed` (`VariableField<float>`), `speedModifier` (`VariableField<float>`), `target` (`VariableField<Vector2|Vector3|UnityObject>`).
- Outputs: none.

The final horizontal speed is `speed * speedModifier`. Flight duration is derived from the solved ballistic trajectory rather than authored separately.

## Success / Failure semantics

- Success after the solved ballistic trajectory reaches its computed end time.
- Failure when target is empty or invalid.

## Important limitations

- The control target must implement `IMovementSource`.
- Requires `Rigidbody2D` and `Collider2D` on the AI host GameObject.
- Uses fixed-timestep movement.
- The trajectory requires downward vertical gravity, positive speed and jump height, positive gravity scale, and zero linear damping.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
