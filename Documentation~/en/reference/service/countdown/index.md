# `Countdown`

## Purpose

Decrement an ordinary Float only while the host branch is registered.

## Key inputs / outputs

- Input: `updatingVariable` (`VariableReference<float>`), which must bind an
  ordinary readable/writable Float.
- Outputs: none.

## Success / Failure semantics

- Remains ready as a service and yields while attached to its host.
- Uses the owning root tree's `timeSettings` and shared timer.
- Settles elapsed time once when the host branch is unregistered.

## Important limitations

- Time spent outside the host branch is not counted.
- `AI` time advances only while the owning root tree is active, healthy, driven,
  enabled, and not paused.
- The root tree owns the shared timer object; reads do not change timer state or
  settle AI lifetime time.
- Pause gates tree-driver callbacks only. It does not freeze external
  asynchronous work, animation, physics, or coroutines.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Countdown.cs)
