# Concepts

## Important Concept

### AI (MonoBehaviour)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

`AI` is the runtime component attached to a GameObject. It holds a `BehaviourTreeData`, creates a runtime `BehaviourTree` in `Start()`, and forwards `Update`, `LateUpdate`, and `FixedUpdate` to the tree.

Common fields:

- `BehaviourTreeData data`: the behaviour tree asset to run.
- `MonoBehaviour controlTarget`: the control script used by component-call nodes and component access. `OnValidate()` tries to bind it from the same GameObject according to the tree asset's `targetScript`.
- `awakeStart`: whether to start automatically when the object enters the scene.
- `autoRestart`: whether to start another tree run from `FixedUpdate` after the current run ends.

The AI Inspector and component context menu provide runtime controls such as `Start Behaviour Tree`, `Reload Behaviour Tree`, `Pause`, `Continue`, and `End`.

### BehaviourTreeData (ScriptableObject)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTreeData.cs)

`BehaviourTreeData` is the behaviour tree asset. Create it through `Create/Aethiumian AI/Behaviour Tree`. It stores:

- `headNodeUUID`: root node UUID.
- `nodes`: all serialized nodes.
- `variables`: the tree variable table.
- `targetScript`, `animatorController`, `prefab`: editor helper data.
- `noActionMaximumDurationLimit`, `actionMaximumDuration`, and error-handling settings.

Edit this asset through AI Editor whenever possible. Inspector serialization is mainly for debugging; the asset Inspector provides an `Open AI Editor` button.

### AIEditorWindow (Editor Window)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

Open AI Editor from `Window/Aethiumian AI/AI Editor`. The outer shell uses UI Toolkit and provides the behaviour tree selector, four pages—Nodes, Graph, Variables, and Properties—a selection lock, and maintenance tools. Nodes, Variables, and Properties remain hosted by Unity's supported `IMGUIContainer`; Graph uses a custom UI Toolkit canvas and one IMGUI inspector for a single selected node. The Graph page supports middle-button or Alt-left pan, zoom, single and box multi-selection, grouped dragging, node search and creation, compatible-port insertion and connection, grouped deletion and duplication, shared subgraph clipboard commands, context menus, and explicit Auto Layout. Control-flow nodes are rendered as compact ordered distributors, branch nodes use branch-gate shapes and separate output ports, services and their subtrees use a side rail, and ordinary action/call nodes remain cards. Graph positions are stored in a separate versioned editor-only layout and lifecycle commands preserve existing coordinates while assigning positions only to new nodes; opening or refreshing a tree does not create an asset diff. Editor preferences are available from `Edit/Preferences/Aethiumian AI/AI Editor` or the AI Editor toolbar `Settings` button. Opening a specific `BehaviourTreeData` reuses the existing editor window for that tree, while different trees can be open in separate editor windows. Node clipboard content is shared between AI Editor windows so copied nodes can be pasted across trees. When no tree is selected, use `Create New Behaviour Tree` to create an asset. If the Unity Selection is a GameObject, the editor tries to add or reuse its `AI` component and assign the new tree when `AI.Data` is empty.

### BehaviourTree (Runtime Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.cs)

`BehaviourTree` is the runtime instance. It clones nodes from `BehaviourTreeData`, builds UUID-to-node references, variable tables, and Unity object references, then executes through `NodeCallStack`.

The runtime tree does not execute asset node instances directly. Put runtime state in runtime nodes, variables, or components instead of assuming the asset nodes are mutated.

### NodeCallStack

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.NodeCallStack.cs)

`NodeCallStack` is the actual execution stack. It advances the current node, receives child returns, waits for actions, handles interruptions, and ends execution. The main behaviour runs on the main stack; services and helper branches such as `Parallel` use additional stacks.

### TreeNode (Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/TreeNode.cs)

`TreeNode` is the base class for all nodes. Node execution uses `State`, which is eventually folded into a boolean return for the parent:

- `true`: the node succeeds or the condition is true.
- `false`: the node fails or the condition is false.
- `Yield` / `NONE_RETURN`: the node has not produced a final result yet, so the tree waits or continues in a later frame.

#### head (root node)

The root node is defined by `BehaviourTreeData.headNodeUUID`. Every tree run starts the main execution stack from this node.

### Variable

Variable definitions live in [VariableType](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Fields/Variables/VariableType.cs). The main variable types are:

| Type                 | VariableType  | Use                    |
| :------------------- | :------------ | :--------------------- |
| `string`             | `String`      | text                   |
| `int`                | `Int`         | integer                |
| `float`              | `Float`       | decimal number         |
| `bool`               | `Bool`        | state                  |
| `Vector2`            | `Vector2`     | 2D vector              |
| `Vector3`            | `Vector3`     | 3D vector              |
| `Vector4` / `Color`  | `Vector4`     | 4D vector or color     |
| `UnityEngine.Object` | `UnityObject` | Unity object reference |
| `object`             | `Generic`     | arbitrary object       |

`Invalid` and `Node` are hidden/internal types and are usually not selected manually in a normal variable table.

Variables with the same name are not allowed in the same tree, even if they have different types. Initial definitions come from the asset; a runtime `BehaviourTree` builds the variable table for the executing instance. Nodes can read, write, or reference those runtime variables.

Common variable field forms:

| Declaration                | Meaning                                                           |
| :------------------------- | :---------------------------------------------------------------- |
| `float`                    | fixed constant                                                    |
| `VariableField<float>`     | float variable or constant                                        |
| `VariableReference<float>` | float variable reference                                          |
| `VariableField`            | any variable or constant; actual valid types depend on node logic |
| `VariableReference`        | any variable reference; actual valid types depend on node logic   |

Even when a non-generic field allows any variable, the node itself may only support specific types. For example, a boolean arithmetic node cannot use a `string` as a boolean argument.
