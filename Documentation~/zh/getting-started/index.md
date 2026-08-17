# 开始使用

## 开始使用

### 创建 BehaviourTreeData

常用方式有两种：

- 在 Project 窗口中使用 `Create/Aethiumian AI/Behaviour Tree` 创建资产。
- 打开 `Window/Aethiumian AI/AI Editor`，在未选择行为树时点击 `Create New Behaviour Tree` 创建资产。

如果在 AI Editor 中创建新资产时当前选中了一个 GameObject，编辑器会尝试自动添加或复用该对象上的 `AI` 组件，并在 `AI.Data` 为空时绑定新建的行为树。

### 打开 AI Editor

可以从以下入口打开：

- Unity 菜单：`Window/Aethiumian AI/AI Editor`。
- 选中 `BehaviourTreeData` 资产，在 Inspector 顶部点击 `Open AI Editor`。
- 选中带有 `AI` 组件的 GameObject，在 AI 组件 Inspector 中点击 `Open Editor`。

打开后，在顶部 `Behaviour Tree` 对象栏选择要编辑的 `BehaviourTreeData`。重复打开同一棵 tree 会聚焦已有 AI Editor 窗口；打开另一棵 tree 会创建或聚焦属于那棵 tree 的窗口。

### 绑定并运行

1. 给需要运行 AI 的 GameObject 添加 `AI` 组件。
2. 把 `BehaviourTreeData` 资产赋给 `AI.Data`。
3. 如果行为树资产设置了 `targetScript`，确认同一个 GameObject 上存在对应组件；`AI.OnValidate()` 会尝试自动绑定到 `ControlTarget`。
4. 需要进场自动启动时保持 `awakeStart` 开启；需要树结束后循环执行时保持 `autoRestart` 开启。
5. 运行中可以通过 AI 组件右键菜单或 Inspector 控制启动、重载、暂停、继续和结束。

### 创建第一棵树

1. 在 AI Editor 中选择或创建一个 `BehaviourTreeData`。
2. 如果还没有根节点，创建一个流程节点作为 head，例如 `Sequence`、`Decision` 或 `Loop`。
3. 在节点编辑器中给 head 添加子节点。
4. 在变量表中添加需要的变量，并在节点字段中用 `VariableField` 或 `VariableReference` 绑定。
5. 保存资产后进入 Play Mode，通过 AI 组件或 AI Runtime Inspector 观察执行状态。
