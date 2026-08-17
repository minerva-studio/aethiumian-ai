# `Timeout`

## Purpose

Interrupt host execution after elapsed configured time.

## Key inputs / outputs

- Inputs: `time` (`VariableField<float>`), `result` (`Failed`/`Success`).
- Outputs: none.

## Success / Failure semantics

- Schedules timeout callback; interrupt result follows configured `result`.

## Important limitations

- Uses fixed delta timing and resets when service is re-registered.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timeout.cs)
