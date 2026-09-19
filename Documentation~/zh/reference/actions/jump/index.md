# `Jump`

## 用途
沿导航规划器提供的抛物线跳跃步骤，朝配置的目标移动实体。

## 关键输入 / 输出
- 通用移动输入：`type`、`path`，以及对应行为选择的目标（`tracing`、`destination` 或漫游设置）；适用时还包括 `goal`、`distanceMetric` 和 `reachDistance`。
- 跳跃参数：`jumpHeight`、`jumpLength`、`jumpInterval` 和 `speedModifier`（用于缩放两次起跳之间的间隔）。
- 输出：无。

## 成功 / 失败语义
- 到达配置的移动目标，且实体正在下降或已获得地面支撑时成功。
- 目标或路线无效，或规划的跳跃无法执行时失败。

## 重要限制
- 需要导航运行时、`Rigidbody2D`、启用的导航碰撞体，以及实现 `IMovementSource` 的控制目标。
- 初始路线要求实体有地面支撑。每次跳跃都从兼容的支撑面起跳，并会等待配置的间隔再进行下一次起跳。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Jump.cs)
