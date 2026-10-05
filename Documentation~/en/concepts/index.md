# Core Concepts

A behaviour tree in Aethiumian.AI is authored as an asset, turned into a runtime instance by a component, and executed node by node on call stacks. This page describes each piece; [Runtime Integration](../runtime-integration/index.md) covers how to drive them from your own code.

## AI (MonoBehaviour)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

`AI` is the runtime component attached to a GameObject. It holds a `BehaviourTreeData`, creates a runtime `BehaviourTree` in `Start()`, and forwards `Update`, `LateUpdate`, and `FixedUpdate` to the tree.

Common fields:

- `BehaviourTreeData data`: the behaviour tree asset to run.
- `MonoBehaviour controlTarget`: the control script used by component-call nodes and component access. `OnValidate()` tries to bind it from the same GameObject according to the tree asset's `targetScript`.
- `awakeStart`: whether to start automatically when the object enters the scene.
- `autoRestart`: whether to start another tree run from `FixedUpdate` after the current run ends.

The AI Inspector and component context menu provide runtime controls such as `Start Behaviour Tree`, `Reload Behaviour Tree`, `Pause`, `Resume`, and `End`. `AI.IsPaused` gates only automatic Unity lifecycle forwarding; it does not freeze physics, coroutines, or animation.

## BehaviourTreeData (ScriptableObject)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTreeData.cs)

`BehaviourTreeData` is the behaviour tree asset. Create it through `Create/Aethiumian AI/Behaviour Tree`. It stores:

- `headNodeUUID`: root node UUID.
- `nodes`: all serialized nodes.
- `variables`: the tree variable table.
- `targetScript`, `animatorController`, `prefab`: editor helper data.
- `noActionMaximumDurationLimit`, `actionMaximumDuration`, and error-handling settings.

Edit this asset through AI Editor whenever possible. Inspector serialization is mainly for debugging; the asset Inspector provides an `Open AI Editor` button.

## AIEditorWindow (Editor Window)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

AI Editor is where `BehaviourTreeData` assets are authored. Each tree opens in its own window. See [AI Editor](../editor/index.md) for its pages and controls.

## BehaviourTree (Runtime Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.cs)

`BehaviourTree` is the runtime instance. It clones nodes from `BehaviourTreeData`, builds UUID-to-node references, variable tables, and Unity object references, then executes through `NodeCallStack`.

The runtime tree does not execute asset node instances directly. Put runtime state in runtime nodes, variables, or components instead of assuming the asset nodes are mutated.

## NodeCallStack

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.NodeCallStack.cs)

`NodeCallStack` is the actual execution stack. It advances the current node, receives child returns, waits for actions, handles interruptions, and ends execution. The main behaviour runs on the main stack; services and helper branches such as `Parallel` use additional stacks.

## TreeNode (Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/TreeNode.cs)

`TreeNode` is the base class for all nodes. Node execution uses `State`, which is eventually folded into a boolean return for the parent:

- `true`: the node succeeds or the condition is true.
- `false`: the node fails or the condition is false.
- `Yield` / `NONE_RETURN`: the node has not produced a final result yet, so the tree waits or continues in a later frame.

### head (root node)

The root node is defined by `BehaviourTreeData.headNodeUUID`. Every tree run starts the main execution stack from this node.

## Variable

Each tree declares a table of named, typed variables. A runtime `BehaviourTree` builds its own copy of that table from the asset, and nodes read, write, or reference those runtime values through `VariableField` and `VariableReference` fields. See [Variables](../variables/index.md) for the supported types and field forms.
