# `PseudoProbability`

## Purpose

Select a child by weighted probability with optional anti-repetition bias.

## Key inputs / outputs

- Inputs: `events` (`EventWeight` list), `maxConsecutiveBranch`, `randomSourceOverride`.
- Outputs: none.

## Success / Failure semantics

- Returns selected child's result and tracks immediate repeat selection.

## Important limitations

- Bias logic is only applied when `maxConsecutiveBranch` is a positive limit.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/PseudoProbability.cs)
