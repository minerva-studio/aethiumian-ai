# 概览

Aethiumian.AI 是面向 Unity 的行为树工具包。你在可视化编辑器中编写行为树，把它挂到 GameObject 上，运行时会随 Unity 生命周期执行它。

## 各部分的关系

| 部分 | 作用 |
| :--- | :--- |
| `BehaviourTreeData` | 行为树资产：节点、变量和执行设置。 |
| AI Editor | 编写行为树资产的编辑器窗口。 |
| `AI` 组件 | 在 GameObject 上运行行为树，并把 Unity 更新转发给它。 |
| `BehaviourTree` | `AI` 组件根据资产构建的运行时实例。 |
| 节点 | 行为树的组成单元，分为下面几类。 |

每个 GameObject 都有自己的一份运行时行为树，所以一个资产可以驱动多个对象。变量属于各自的实例；例外是 Static 变量，由同一资产的所有实例共享，以及 Global 变量，由所有行为树共享。

## 节点分类

| 分类 | 用途 |
| :--- | :--- |
| [流程节点](../reference/flow.md) | 控制运行哪些子节点、按什么顺序运行，例如 `Sequence`、`Decision`、`Loop`。 |
| [行动节点](../reference/actions.md) | 持续一段时间、稍后才结束的行为，例如 `Walk`、`Idle`、`PlayAnimationWait`。 |
| [调用节点](../reference/calls.md) | 对 Unity 对象和你自己的脚本执行一步操作。 |
| [判断节点](../reference/determines.md) | 产生一个值或真/假结果的条件与查询。 |
| [运算节点](../reference/arithmetic.md) | 对变量做数学和逻辑运算。 |
| [装饰器节点](../reference/decorator.md) | 包装一个子节点，改变或观察它的结果。 |
| [服务节点](../reference/service.md) | 与子树并行运行，例如中断子树或倒计时。 |

你也可以添加自己的节点，见[自定义节点](../custom-nodes/index.md)。

## 下一步

- 第一次使用：先看[安装](../installation/index.md)，再看[开始使用](../getting-started/index.md)。
- 理解运行模型：[核心概念](../concepts/index.md)和[变量系统](../variables/index.md)。
- 在代码中驱动行为树：[运行时集成](../runtime-integration/index.md)。
- 遇到问题：[故障排查](../debugging/index.md)。
