# `SignChange`

## Purpose

Check value sign change relative to threshold and write a boolean flag.

## Key inputs / outputs

- Inputs: `value`, `bound`, `determine`, `baseValue`, `change`.
- Outputs: `change` bool.

## Success / Failure semantics

- Success on positive/negative transition.

## Important limitations

- Returns failure when no transition is detected.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/SignChange.cs)
