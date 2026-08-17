# AI 编辑器

从 `Window > Aethiumian AI > AI Editor` 打开编辑器，然后选择一个 `BehaviourTreeData` 资产。工具栏提供 Graph、Nodes、Variables、Properties 四个页面，以及刷新、设置和维护命令。

## Graph 图表

![AI 编辑器图表](../../assets/images/ai-editor-graph.png)

Graph 页面在同一工作区中显示可达执行流程和当前节点的属性面板，支持：

- 使用鼠标中键或 Alt + 左键平移，滚轮缩放；
- 单选、框选、成组拖动、复制和删除；
- 搜索并创建节点、从兼容端口插入和连接；
- 右键菜单、跨编辑器窗口共享剪贴板，以及显式 Auto Layout。

选择节点后，可在右侧面板编辑字段。控制流节点显示有序输出，分支节点显示独立结果路径，Service 则沿宿主子树的侧轨显示。

## Variables 变量

![AI 编辑器变量表](../../assets/images/ai-editor-variables.png)

Variables 页面定义节点可访问的树级变量。每行包含名称、类型、默认值、作用域和 static 选项；同一棵树内变量名称必须唯一。

## Properties 属性

![AI 编辑器属性](../../assets/images/ai-editor-properties.png)

Properties 页面用于配置目标脚本、目标 Prefab、随机源、作用域、Action 超时和错误处理策略等集成与执行设置。

Graph 坐标保存在独立的编辑器布局中。打开或刷新行为树会保留已有坐标，不应修改行为树资产。
