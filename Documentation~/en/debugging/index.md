# Troubleshooting

![Runtime AI Inspector](../../assets/images/ai-runtime-inspector.png)

## Quick checklist

- Verify the target `GameObject` has an `AI` component with a valid `BehaviourTreeData` reference.
- Confirm the expected `AI` instance is selected and currently running in Play Mode.
- If a tree was edited during runtime, call `Reload Behaviour Tree` before re-testing.
- Confirm the tree that is currently active is the one you expect to observe.
- Open the Console first and check for import/runtime warnings before changing logic.

## AI Inspector and runtime controls

The AI Inspector and component context menu expose the following runtime controls: `Start Behaviour Tree`, `Reload Behaviour Tree`, `Pause`, `Continue`, and `End`.

Use these controls from the live GameObject that owns the active behaviour tree.

## Reload, Pause, Continue, Restart semantics

- `Reload Behaviour Tree`: ends the current runtime tree instance and recreates the tree from its serialized asset data. If `autoRestart` is false, this does not auto-start execution. If `autoRestart` is true, the AI runtime may start again according to that setting.
- `Pause`: keeps the current execution state and stops ticking until resumed.
- `Continue`: resumes ticking from the paused state and continues execution.
- `Restart` (editor/runtime restart action): performs a `Reload Behaviour Tree` operation.

In addition to controls:

- `Pause` flow node: execution halts at the node point that enters pause and waits for external resume logic.
- `Restart` flow node: reloads the currently running tree and replaces the active stack.

## DebugPrint and DebugPrintf

- `DebugPrint` writes a basic message into Unity Console.
- `DebugPrintf` writes a formatted message into Unity Console.
- Both are diagnostic tools; they do not alter flow semantics by themselves.
- Both nodes return their configured `returnValue` per their reference definitions.

## Common symptom table

| Symptom | Likely cause | First action |
| --- | --- | --- |
| No behaviour execution in Play Mode | Missing or invalid tree binding | Verify `AI` component and assigned `BehaviourTreeData`, then start the tree.
| Tree appears paused | `Pause` control or pause node is active | Check Pause/Continue state and expected control path.
| Debug log not printed | Node not reached in current branch | Validate branch condition and parent execution result.
| Unexpected formatted output | `DebugPrintf` format/value mismatch | Match format specifier to value type and input order.
| Repeating wrong behavior after edit | Runtime instance not rebuilt | Apply `Reload Behaviour Tree` and retest.

## Console and breakpoint workflow

1. Open Console and filter `Error`/`Exception` first.
2. Trace the first failing call stack for the relevant `AI` instance.
3. For method-call failures, set breakpoints on the reflected method and repro once.
4. Confirm method signatures and parameters match selected nodes.
5. Re-run with a fresh `Reload Behaviour Tree` and compare behavior against baseline.
