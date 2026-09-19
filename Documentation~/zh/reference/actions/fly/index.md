# `Fly`

## 用途
通过导航规划器提供的空中航点，朝目标、撤退位置或漫游目的地移动实体。

## 关键输入 / 输出
- 通用移动输入：`type`、`path`，以及对应行为选择的目标（`tracing`、`destination` 或漫游设置）；适用时还包括 `goal`、`distanceMetric` 和 `reachDistance`。
- 飞行参数：`speed`、`speedModifier`、`flexibility`、`maxHeight` 和 `neverAboveMaxHeight`；`setFinalPosition` 控制漫游成功后的最终位置。
- 输出：无。

## 成功 / 失败语义
- 到达配置的移动目标时成功。
- 目标无效、路线无法执行或移动动作无法继续时失败。

## 重要限制
- 需要导航运行时、`Rigidbody2D`、启用的导航碰撞体，以及实现 `IMovementSource` 的控制目标。
- `neverAboveMaxHeight` 仅作用于追踪目标。`maxHeight` 限制目标相对其下方支撑面的高度，不是全局世界高度上限。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Fly.cs)
