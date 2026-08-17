# 运行时集成

### AI (MonoBehaviour)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

`AI` 是挂在 GameObject 上的运行组件。它持有 `BehaviourTreeData`，在 `Start()` 中创建运行时 `BehaviourTree`，并把 `Update`、`LateUpdate`、`FixedUpdate` 转发给行为树。

常用字段：

- `BehaviourTreeData data`：要运行的行为树资产。
- `MonoBehaviour controlTarget`：节点调用方法和读取组件时优先使用的控制脚本。`OnValidate()` 会根据行为树资产的 `targetScript` 尝试自动绑定同 GameObject 上的组件。
- `awakeStart`：进入场景后是否自动启动。
- `autoRestart`：行为树结束后是否在后续 `FixedUpdate` 中自动重新开始。

`AI` 的 Inspector 和组件右键菜单提供运行时控制入口，包括 `Start Behaviour Tree`、`Reload Behaviour Tree`、`Pause`、`Continue`、`End`。

### BehaviourTreeData (ScriptableObject)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTreeData.cs)

`BehaviourTreeData` 是行为树资产，通过 `Create/Aethiumian AI/Behaviour Tree` 创建。它保存：

- `headNodeUUID`：根节点 UUID。
- `nodes`：所有序列化节点。
- `variables`：该行为树的变量表。
- `targetScript`、`animatorController`、`prefab`：编辑器辅助信息。
- `noActionMaximumDurationLimit`、`actionMaximumDuration`、错误处理策略等运行设置。

请优先通过 AI Editor 编辑该资产。Inspector 里的序列化字段主要用于调试；Inspector 顶部提供 `Open AI Editor` 按钮，可以直接打开当前资产。

### AIEditorWindow (Editor Window)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Editor/AIEditorWindow/AIEditorWindow.cs)

AI Editor 的入口是 `Window/Aethiumian AI/AI Editor`。窗口外层使用 UI Toolkit，提供行为树选择栏、Nodes、Graph、Variables、Properties 四个页面标签、锁定选择按钮和维护工具。Nodes、Variables、Properties 仍通过 Unity 官方支持的 `IMGUIContainer` 承载；Graph 使用自研 UI Toolkit canvas，并只为单个选中节点建立 IMGUI 属性面板。Graph 现在支持中键或 Alt+左键平移、缩放、单选与框选多选、成组拖动布局、节点搜索与创建、从兼容端口插入并连接、批量删除与复制、共享子图 clipboard 粘贴、右键菜单和显式 Auto Layout。Sequence、Parallel 等控制流节点显示为带有有序输出端口的紧凑分发门，Decision、Condition、Probability 等显示为分支门，Service 及其子树位于宿主侧轨，只有 Action、Call 等普通节点保留卡片形态。Graph 坐标保存在独立的版本化编辑器布局中，生命周期命令会保留已有节点坐标，只为新节点写入位置；打开或刷新行为树不会产生 asset diff。Graph 已支持 Entrance/Exit 边界、Set as Head 和有序引用 Reorder；已有的 Service→Service 数据仍可读取，但编辑器不允许再创建或替换出新的 Service→Service 引用。编辑器偏好设置可以从 `Edit/Preferences/Aethiumian AI/AI Editor` 打开，也可以通过 AI Editor 工具栏里的 `Settings` 按钮跳转。打开指定 `BehaviourTreeData` 时会复用该 tree 已有的 editor window，不同 tree 可以同时打开在不同窗口中。节点 clipboard 在所有 AI Editor 窗口之间共享，因此可以跨 tree 复制粘贴节点。没有选中行为树时，可以在窗口中使用 `Create New Behaviour Tree` 创建新资产；如果当前 Unity Selection 是 GameObject，编辑器会尝试给它添加或复用 `AI` 组件并绑定新资产。

### BehaviourTree (Runtime Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.cs)

`BehaviourTree` 是运行时实例。它从 `BehaviourTreeData` 克隆节点，生成 UUID 到节点的引用表，构建变量表和 Unity Object 引用，然后通过 `NodeCallStack` 执行。

行为树不会直接运行资产中的节点实例，因此运行时状态应放在运行时节点、变量或组件上，而不是假设资产节点本身会被修改。

### NodeCallStack

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Tree/BehaviourTree.NodeCallStack.cs)

`NodeCallStack` 是实际执行栈。它负责推进当前节点、接收子节点返回值、等待 Action、处理中断和结束。主行为由 main stack 执行；Service 和 `Parallel` 等辅助分支会使用额外的 stack。

### TreeNode (Class)

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/TreeNode.cs)

`TreeNode` 是所有节点的基类。节点执行结果使用 `State` 表示，最终会向父节点折算为布尔返回值：

- `true`：节点成功或判断为真。
- `false`：节点失败或判断为假。
- `Yield` / `NONE_RETURN`：节点尚未给出最终返回值，行为树会继续等待或在后续帧推进。

#### 头(根节点)

根节点由 `BehaviourTreeData.headNodeUUID` 指定。每次行为树启动时，主执行栈都会从根节点开始。

### Variable 变量

变量定义位于 [VariableType](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Fields/Variables/VariableType.cs)。当前主要变量类型如下：

| 类型                 | VariableType  | 作用           |
| :------------------- | :------------ | :------------- |
| `string`             | `String`      | 文本           |
| `int`                | `Int`         | 整数           |
| `float`              | `Float`       | 小数           |
| `bool`               | `Bool`        | 状态           |
| `Vector2`            | `Vector2`     | 二维向量       |
| `Vector3`            | `Vector3`     | 三维向量       |
| `Vector4` / `Color`  | `Vector4`     | 四维向量或颜色 |
| `UnityEngine.Object` | `UnityObject` | Unity 对象引用 |
| `object`             | `Generic`     | 任意对象       |

`Invalid` 和 `Node` 是内部/隐藏类型，通常不在普通变量表中手动选择。

同一个行为树中不允许出现同名变量，即使类型不同。变量的初始定义来自资产，运行时 `BehaviourTree` 会为执行实例构建变量表；节点可以读取、写入或引用这些运行时变量。

Variable 在节点字段中常见的几种写法：

| 声明                       | 解释                                       |
| :------------------------- | :----------------------------------------- |
| `float`                    | 固定常量                                   |
| `VariableField<float>`     | float 变量或常量                           |
| `VariableReference<float>` | float 变量引用                             |
| `VariableField`            | 任意变量或常量，实际可用类型由节点逻辑决定 |
| `VariableReference`        | 任意变量引用，实际可用类型由节点逻辑决定   |

即使 Non-Generic 字段允许选择任意变量，节点自身仍可能只支持某些类型。例如布尔运算节点不能把 `string` 当作布尔参数使用。
