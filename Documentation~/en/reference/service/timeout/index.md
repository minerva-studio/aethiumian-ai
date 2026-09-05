# `Timeout`

## Purpose

Interrupt host execution after elapsed configured time.

## Key inputs / outputs

- Inputs: `time` (`VariableField<float>`) and `result` (`Failed`/`Success`).
- Outputs: none.

## Success / Failure semantics

- Interrupts the host with the configured result after the deadline.
- Uses the owning root tree's `timeSettings` with independent Game/AI and
  Scaled/Unscaled choices; zero values mean Game + Scaled.

## Important limitations

- Keeps the existing service-check schedule. Unscaled time can advance while
  `timeScale` is zero, but it does not make service checks run during a paused
  AI or a stopped driver.
- A tree-owned timeout reads the root timer. Pause
  does not freeze an external asynchronous function; it defers this service
  check until the tree driver resumes.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timeout.cs)
