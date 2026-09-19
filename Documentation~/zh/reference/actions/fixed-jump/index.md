# `FixedJump`

## 用途
朝配置的点或物体执行一次抛物线跳跃。节点可以直接求解落点，也可以请求导航规划器提供一个跳跃步骤。

## 关键输入 / 输出
- 输入：`target`（包含 `Vector2`、`Vector3` 或 Unity 物体的 `VariableField`）、`jumpHeight`、`jumpLength`，以及可选的 `offset`。
- 选项：`targetMode`（`Direct` 或 `PlannedStep`）、规划目标的 `targetMeasurement`、`goal`、`distanceMetric`、`reachDistance` 和 `skipReached`。
- 输出：无。

## 成功 / 失败语义
- 跳跃执行器完成时成功；启用 `skipReached` 且目标已经满足配置的目标条件时，也会立即成功。
- 目标或跳跃参数无效、导航无法提供可用跳跃，或执行失败时失败。

## 重要限制
- 需要导航运行时、`Rigidbody2D` 和启用的导航碰撞体。按碰撞体边界测量的物体目标必须带有碰撞体。
- `PlannedStep` 使用规划器的下一段跳跃路线；`Direct` 则直接求解到目标落点的弹道。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
