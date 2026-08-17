# `Branch`

## Purpose

Start and run a service subtree branch from the host node's service slot.

## Key inputs / outputs

- Inputs: `subtreeHead` (`NodeReference`).
- Outputs: none.

## Success / Failure semantics

- Success when branch starts and finishes.
- Fails when branch head is missing or branch execution fails.

## Important limitations

- Subtree is governed by service life cycle of the host.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Branch.cs)
