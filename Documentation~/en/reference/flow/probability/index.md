# `Probability`

## Purpose

Select one child by weighted probability and execute it.

## Key inputs / outputs

- Inputs: `events` (`EventWeight` list containing node+weight).
- Outputs: none.

## Success / Failure semantics

- Returns based on the executed child result.

## Important limitations

- Zero or missing weights may skip valid selection depending on resolver behavior.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Probability.cs)
