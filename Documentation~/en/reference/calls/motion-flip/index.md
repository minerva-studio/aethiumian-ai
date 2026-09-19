# `MotionFlip`

## Purpose

Updates the local `SpriteRenderer` horizontal flip from the host's horizontal `Rigidbody2D` velocity.

## Key inputs / outputs

- Inputs: the host's `Rigidbody2D` and `SpriteRenderer` components.
- Outputs: updates `SpriteRenderer.flipX` when horizontal speed is above the node's motion threshold.

## Success / Failure semantics

- Success when both required components are present; horizontal motion to the left sets `flipX`, and motion to the right clears it.
- Failure when either required component is missing.

## Important limitations

- Horizontal velocity at or below the threshold leaves the current flip unchanged.
- The node only changes the local renderer and does not rotate or move the object.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/MotionFlip.cs)
