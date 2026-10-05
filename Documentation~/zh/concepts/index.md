# 核心概念

在 Aethiumian.AI 中，行为树以资产的形式编写，由组件转换为运行时实例，再在调用栈上逐个节点执行。本页介绍每个部分；如何在自己的代码中驱动它们，见[运行时集成](../runtime-integration/index.md)。

## AI (MonoBehaviour)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

`AI` 是挂在 GameObject 上的运行组件。它持有 `BehaviourTreeData`，在 `Start()` 中创建运行时 `BehaviourTree`，并把 `Update`、`LateUpdate`、`FixedUpdate` 转发给行为树。

常用字段：

- `BehaviourTreeData data`：要运行的行为树资产。
- `MonoBehaviour controlTarget`：节点调用方法和读取组件时优先使用的控制脚本。`OnValidate()` 会根据行为树资产的 `targetScript` 尝试自动绑定同 GameObject 上的组件。
- `awakeStart`：进入场景后是否自动启动。
- `autoRestart`：行为树结束后是否在后续 `FixedUpdate` 中自动重新开始。

`AI` 的 Inspector 和组件右键菜单提供运行时控制入口，包括 `Start Behaviour Tree`、`Reload Behaviour Tree`、`Pause`、`Resume`、`End`。`AI.IsPaused` 只控制 Unity 自动生命周期转发，不会冻结物理、协程或动画。

## BehaviourTreeData (ScriptableObject)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTreeData.cs)

`BehaviourTreeData` 是行为树资产，通过 `Create/Aethiumian AI/Behaviour Tree` 创建。它保存：

- `headNodeUUID`：根节点 UUID。
- `nodes`：所有序列化节点。
- `variables`：该行为树的变量表。
- `targetScript`、`animatorController`、`prefab`：编辑器辅助信息。
- `noActionMaximumDurationLimit`、`actionMaximumDuration`、错误处理策略等运行设置。

请优先通过 AI Editor 编辑该资产。Inspector 里的序列化字段主要用于调试；Inspector 顶部提供 `Open AI Editor` 按钮，可以直接打开当前资产。

## AIEditorWindow (Editor Window)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

AI Editor 用于编写 `BehaviourTreeData` 资产，每棵树在各自的窗口中打开。页面和操作见 [AI 编辑器](../editor/index.md)。

## BehaviourTree (Runtime Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.cs)

`BehaviourTree` 是运行时实例。它从 `BehaviourTreeData` 克隆节点，生成 UUID 到节点的引用表，构建变量表和 Unity Object 引用，然后通过 `NodeCallStack` 执行。

行为树不会直接运行资产中的节点实例，因此运行时状态应放在运行时节点、变量或组件上，而不是假设资产节点本身会被修改。

## NodeCallStack

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.NodeCallStack.cs)

`NodeCallStack` 是实际执行栈。它负责推进当前节点、接收子节点返回值、等待 Action、处理中断和结束。主行为由 main stack 执行；Service 和 `Parallel` 等辅助分支会使用额外的 stack。

## TreeNode (Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/TreeNode.cs)

`TreeNode` 是所有节点的基类。节点执行结果使用 `State` 表示，最终会向父节点折算为布尔返回值：

- `true`：节点成功或判断为真。
- `false`：节点失败或判断为假。
- `Yield` / `NONE_RETURN`：节点尚未给出最终返回值，行为树会继续等待或在后续帧推进。

### 头(根节点)

根节点由 `BehaviourTreeData.headNodeUUID` 指定。每次行为树启动时，主执行栈都会从根节点开始。

## Variable 变量

每棵树声明一张带名称和类型的变量表。运行时 `BehaviourTree` 会根据资产构建自己的变量表，节点通过 `VariableField` 和 `VariableReference` 字段读取、写入或引用这些运行时变量。支持的类型和字段写法见[变量系统](../variables/index.md)。
