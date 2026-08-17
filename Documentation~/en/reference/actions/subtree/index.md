# `Subtree`

## Purpose

Execute a referenced behaviour-tree data asset as a nested tree.

## Key inputs / outputs

- Inputs: `behaviourTreeData`, `variableTable` mappings.
- Outputs: inherited child result.

## Success / Failure semantics

- Success/failure is inherited from subtree final result.
- Failure when subtree cannot initialize or execute.

## Important limitations

- Parent and child variables are connected via translation table only.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Subtree.cs)
