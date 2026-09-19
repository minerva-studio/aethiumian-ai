# `ValueChanged`

## Purpose

Detects whether a referenced variable's current value differs from its previous observation.

## Key inputs / outputs

- Input: `value` (`VariableReference`), which must resolve to a value.
- Output: a boolean determination.

## Success / Failure semantics

- The first observation stores a baseline and returns false. Later observations return true once when the captured value changes, then update the baseline.
- An unset variable reference is an invalid node.

## Important limitations

- The comparison is against the immediately previous observation, not the value when the behaviour tree started.
- The previous-value snapshot is runtime state and is reset when the node initializes.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/ValueChanged.cs)
