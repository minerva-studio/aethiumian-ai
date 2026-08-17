# `WaitForDestroy`

## Purpose

Keep waiting until a referenced Unity object is destroyed.

## Key inputs / outputs

- Inputs: `value` (`VariableReference<UnityEngine.Object>`).
- Outputs: none.

## Success / Failure semantics

- Success once the tracked reference becomes null.

## Important limitations

- No explicit failure branch is exposed by this node.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/WaitForDestroy.cs)
