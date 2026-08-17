# `Always`

## Purpose

Execute a fixed child node and return a fixed boolean value.

## Key inputs / outputs

- Inputs: `node` (`TreeNode`), `returnValue` (`VariableField<bool>`).
- Outputs: none.

## Success / Failure semantics

- Returns the configured `returnValue`.

## Important limitations

- The child execution result is ignored by this node.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Always.cs)
