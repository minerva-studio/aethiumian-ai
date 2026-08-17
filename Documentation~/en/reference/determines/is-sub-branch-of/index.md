# `IsSubBranchOf`

## Purpose

Determine whether current node is inside a referenced branch.

## Key inputs / outputs

- Inputs: `root` (`RawNodeReference`).
- Outputs: bool.

## Success / Failure semantics

- Returns `true` if current stage node is equal to or descendant of `root`.

## Important limitations

- Invalid references are treated as false.

## Source code

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsSubBranchOf.cs)
