# Aethiumian.AI Graph 编辑器开发流程

本页定义 Aethiumian.AI Graph Editor 的开发与验证流程，目标是在保留交付门槛的前提下提高反馈速度。

## 修改前检查

1. 确认行为语义、所属边界、受影响的表现层类型与验收标准。
2. 检查当前包内变更范围并保留无关资产。
3. 将实现与聚焦测试打包在同一批次提交，避免在完整回归后才补普通用例。

## 仅 USS 的视觉迭代

当修改 `AI Editor Graph` 的颜色、透明度、线宽、图案等 `.ai-editor-graph-canvas` 自定义属性时，按该路径处理。

1. 编辑 `AIEditorWindow.uss`，等待 Unity 导入样式表完成。
2. 在已打开的 Graph 页面中选取一个代表性树进行观察；若 live visual tree 未刷新，关闭后重开该标签页。
3. 不要为每次视觉微调发起脚本重编译，也不要运行拓扑测试。USS 导入不应触发程序集重载。
4. 视觉稳定后，只运行覆盖样式解析且不修改资产的最窄窗口冒烟测试。

节点尺寸、表现几何、布局间距、拓扑与执行语义属于 C# 范畴，这类改动不走 USS-only 路径。

## C# 可视化或交互迭代

1. 在请求重编译前，完成一组完整实现和聚焦测试。
2. 重编译一次并等待终态结果。
3. 先发现并执行新增或受影响的方法级测试。
4. 这组方法通过后，再运行最小受影响套件，例如对应 `Aethiumian.AI.Editor.Tests.Graph.GraphCanvasInteractionTests` 的 Graph Editor 夹具。
5. 失败时先修复并复测该方法，确认失败稳定后再重跑套件。
6. 文档-only 或仅 test-only 的修正，若不影响生产代码且先前结果仍有效，不要重启完整 Editor 程序集。

## 提交与交付门槛

生产改动提交前或任务准备交付时，按以下门槛执行一次：

1. 确认受影响 Editor 测试可被发现。
2. 若变更涉及 Graph 表现、布局或交互，运行受影响的 Graph Editor 夹具；跨域 Graph 变更使用 `GraphEditor` 分类。
3. 若改动影响 shell、选择、生命周期或窗口归属，运行 `AIEditorWindowMultiTreeTests`。
4. 运行完整 `Aethiumian.AI.Editor.Tests` 程序集一次。
5. 运行后清理预期日志，等待编辑器空闲，再检查结构化 Console 错误。
6. 对比运行前后 AI Editor 窗口计数；测试必须只关闭由测试自己创建并通过 `EditorWindow.Close()` 释放的窗口。
7. 分离审查最终包 diff 与根项目 diff，避免把无关 AI 资产夹带入包提交。

任何后续生产代码改动都会使该门槛失效并要求重新执行；文档-only 改动可跳过。

## Graph Editor 夹具选择

- 拓扑构建与端口：`GraphTopologyBuilderTests`。
- 拓扑编辑、生命周期、连接、菜单：`GraphTopologyEditTests`。
- 基础表现与节点尺寸：`GraphPresentationTests`。
- 复合表现族：`GraphCompositePresentationTests`。
- 布局、移动、吸附、对齐、分布：`GraphLayoutTests`。
- 画布交互、节点面板、视图控制、键盘导航：`GraphCanvasInteractionTests`。
- 剪贴板、重复、删除与跨树事务：`GraphClipboardTests`。

七类夹具统一归入 `GraphEditor` 分类。多域 Graph 改动可运行该分类；单域的内循环修复或仅测试变更优先只跑受影响夹具。

## 证据上报

分项报告以下边界：

- 脚本编译；
- 测试发现；
- 精准聚焦用例执行；
- 受影响套件执行；
- 完整 Editor 程序集执行；
- 回到空闲后结构化 Console 状态；
- 人工可视化验收。

初始异步结果若未实际执行测试（`executed test count = 0`），只能算任务排队，不是执行证据。静态检查与测试发现均不等同于运行证据。

## 预期反馈时长

- 仅 USS 的绘制变更应以样式导入和视觉检查为主，不应强行触发 C# 重编译。
- 小规模 C# Graph 变更一般应在开发期间执行一次编译 + 精准测试 + 一个受影响套件。
- 完整 Editor 程序集执行是最终交付门槛，不是内循环命令。
