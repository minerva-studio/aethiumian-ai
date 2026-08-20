# `Retry`

## Purpose

Retry one child after failures, up to a fixed number of total attempts.

## Key inputs / outputs

- Inputs: `node` (`TreeNode`), `maxAttempts` (`VariableField<int>`).
- Outputs: none.

## Success / Failure semantics

- A missing child fails.
- A maximum of zero attempts fails without executing the child.
- A successful child attempt succeeds the Retry immediately.
- Failed attempts continue until `maxAttempts` is reached.
- Retry fails when every allowed attempt fails.

## Important limitations

- The attempt limit is read once at the start of each Retry execution.
- `maxAttempts` includes the first attempt and is clamped to zero or greater.
- Retry does not catch exceptions; existing node and tree error policies remain in control.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Retry.cs)
