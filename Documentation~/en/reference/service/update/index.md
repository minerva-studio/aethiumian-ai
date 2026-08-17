# `Update`

## Purpose

Repeatedly execute a subtree as a periodic service action.

## Key inputs / outputs

- Inputs: `interval` (`int`), `forceStopped` (`VariableField<bool>`), `subtreeHead` (`NodeReference`).
- Outputs: none.

## Success / Failure semantics

- Success when subtree starts and can complete successfully.
- Fails when subtree is missing or returns false.

## Important limitations

- `forceStopped` controls whether new routine can override a still-running one.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Update.cs)
