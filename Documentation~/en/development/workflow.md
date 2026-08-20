# Graph Editor Development Workflow

This document defines the development and validation workflow for the
Aethiumian.AI Graph Editor. Its purpose is to keep feedback fast without
weakening the final delivery gate.

## Before Editing

1. Confirm the behavior, ownership boundary, affected presentation types, and acceptance criteria before changing code.
2. Inspect the current package diff and preserve unrelated project assets.
3. Prepare the implementation and its focused tests in the same batch. Do not defer ordinary test coverage until after a full regression run.

## USS-Only Visual Iteration

Use this path when changing graph colors, opacity, line width, or pattern values declared as custom properties on `.ai-editor-graph-canvas`.

1. Edit `AIEditorWindow.uss` and wait for Unity to import the stylesheet.
2. Inspect a representative large AI in an already open Graph tab. Reopen the tab if Unity has not refreshed the live visual tree.
3. Do not request script recompilation or run topology tests for each visual adjustment. USS import must not cause an assembly reload.
4. Once the visual result is stable, run the narrowest existing window smoke test that covers style resolution and non-dirtying behavior.

Node sizes, presentation geometry, layout spacing, topology, and execution
semantics remain C# concerns. Changes to those values do not use this path.

## C# Visual or Interaction Iteration

1. Complete one coherent implementation batch, including focused tests, before requesting recompilation.
2. Recompile once and wait for a terminal result.
3. Discover and run the exact new or affected test methods first.
4. After those methods pass, run the smallest affected suite, such as the affected Graph Editor fixture, such as `Aethiumian.AI.Editor.Tests.Graph.GraphCanvasInteractionTests`.
5. If a test fails, fix and rerun that exact test. Rerun the suite only after the focused failure is stable.
6. Do not restart the full Editor assembly after documentation-only or test-only corrections when the final production code is unchanged and still covered by the previous result.

## Commit and Delivery Gate

Run the following gate once when a production change is ready to commit or the current task is ready for delivery:

1. Confirm test discovery for the affected Editor tests.
2. Run the affected Graph Editor fixture when graph presentation, layout, or interaction changed. Use the `GraphEditor` category for a multi-domain Graph delivery gate.
3. Run `AIEditorWindowMultiTreeTests` when shell, selection, lifecycle, or window ownership changed.
4. Run the complete `Aethiumian.AI.Editor.Tests` assembly once.
5. Clear expected test logs after the run, wait for the Editor to become idle, and inspect structured Console errors once.
6. Compare the AI Editor window count with the pre-run baseline. Tests must close only the windows they created through `EditorWindow.Close()`.
7. Review the final package diff and the root project diff separately. Never include unrelated AI assets in a package commit.

Every subsequent production-code change invalidates the gate and requires one new final run. Documentation-only changes do not.

## Graph Editor Fixture Selection

- Topology construction and ports: `GraphTopologyBuilderTests`.
- Topology edits: `GraphTopologyReferenceEditTests`, `GraphCollectionEditTests`, and `GraphRedirectValidationTests`.
- Decorator edits: `GraphDecoratorEditTests`.
- Node lifecycle: `GraphNodeLifecycleEditTests`.
- Menus and commands: `GraphCommandMenuTests`.
- Basic presentation and node sizing: `GraphPresentationTests`.
- Composite presentation families: `GraphCompositePresentationTests`.
- Layout, movement, snapping, alignment, and distribution: `GraphLayoutTests`.
- Canvas interaction, palette, view controls, and keyboard navigation: `GraphCanvasInteractionTests`.
- Clipboard, duplicate, delete, and cross-tree transactions: `GraphClipboardTests`.

All seven fixtures use the `GraphEditor` category. Run the category for a
multi-domain Graph change; run only the affected fixture for an inner-loop
test-only or single-domain change.

## Evidence Reporting

Report these boundaries separately:

- script compilation;
- test discovery;
- exact focused test execution;
- affected-suite execution;
- full Editor assembly execution;
- structured Console state after returning to idle;
- manual visual inspection.

An initial asynchronous result with zero executed tests is only a queued run,
not test evidence. Static inspection and test discovery are not execution evidence.

## Expected Feedback Time

- USS-only paint changes should require asset import and visual inspection, not C# compilation.
- A small C# graph change should normally require one compile, focused tests, and one affected suite during development.
- The complete Editor assembly is a final gate, not an inner-loop command.
