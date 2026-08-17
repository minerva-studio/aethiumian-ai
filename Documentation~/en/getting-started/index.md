# Getting Started

## Get Started

### Create BehaviourTreeData

There are two common ways:

- In the Project window, use `Create/Aethiumian AI/Behaviour Tree`.
- Open `Window/Aethiumian AI/AI Editor`, then click `Create New Behaviour Tree` when no tree is selected.

When a new asset is created from AI Editor and the current Unity Selection is a GameObject, the editor tries to add or reuse an `AI` component on that object and assign the new tree when `AI.Data` is empty.

### Open AI Editor

You can open it from:

- Unity menu: `Window/Aethiumian AI/AI Editor`.
- Select a `BehaviourTreeData` asset and click `Open AI Editor` at the top of its Inspector.
- Select a GameObject with an `AI` component and click `Open Editor` in the AI component Inspector.

After opening the window, choose the target `BehaviourTreeData` in the top `Behaviour Tree` object field. Reopening the same tree focuses its existing AI Editor window; opening another tree creates or focuses that tree's own window.

### Bind And Run

1. Add an `AI` component to the GameObject that should run the tree.
2. Assign the `BehaviourTreeData` asset to `AI.Data`.
3. If the tree asset sets `targetScript`, make sure the same GameObject has that component; `AI.OnValidate()` tries to bind it to `ControlTarget`.
4. Keep `awakeStart` enabled when the tree should start automatically on scene entry; keep `autoRestart` enabled when the tree should loop after it ends.
5. During Play Mode, use the AI component context menu or Inspector controls to start, reload, pause, continue, or end execution.

### Create The First Tree

1. Select or create a `BehaviourTreeData` in AI Editor.
2. If there is no root node, create a flow node as the head, such as `Sequence`, `Decision`, or `Loop`.
3. Add child nodes to the head in the node editor.
4. Add required variables in the variable table, then bind node fields through `VariableField` or `VariableReference`.
5. Save the asset, enter Play Mode, and observe execution through the AI component or AI Runtime Inspector.
