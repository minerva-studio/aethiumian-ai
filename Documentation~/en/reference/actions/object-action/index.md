# `ObjectAction`

## Purpose

Execute a method on a specific object with action-style completion.

## Key inputs / outputs

- Inputs: `object` (`VariableReference`), `type` (`GenericTypeReference`), `methodName`, `parameters`, `actionCallTime`, `duration`, `count`, `endType`, `result`.
- Outputs: `result` (`VariableReference`) of the called method when available.

## Success / Failure semantics

- Success is determined by action completion rules and called method return.
- Failure when method resolution or invocation is invalid.

## Important limitations

- Reflection invocation requires valid signature and target type.
- Method is treated as action only when action method rule is satisfied.

## Source code

[Source](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/ObjectAction.cs)
