# `Walk`

## 用途
使用规划出的地面移动、跳跃、坠落或穿越平台步骤，朝配置的目标移动实体。

## 关键输入 / 输出
- 通用移动输入：`type`、`path`，以及对应行为选择的目标（`tracing`、`destination` 或漫游设置）；适用时还包括 `goal`、`distanceMetric` 和 `reachDistance`。
- 行走参数：`speed`、`speedModifier`、`accelerateRate`、`jumpHeight`、`jumpLength`；`setFinalPosition` 控制漫游成功后的最终位置。
- 输出：无。

## 成功 / 失败语义
- 到达配置的移动目标时成功。
- 目标或路线无效、无法准备路线步骤或移动无法继续时失败。

## 重要限制
- 需要导航运行时、`Rigidbody2D`、启用的导航碰撞体，以及实现 `IMovementSource` 的控制目标。
- 跳跃步骤要求存在有效的起跳支撑面，且配置的跳跃参数能够求解出可用轨迹。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Walk.cs)
