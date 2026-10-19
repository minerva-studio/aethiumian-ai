# `FixedJump`

## 用途
朝配置的点或物体执行一次抛物线跳跃。节点可以直接求解落点，也可以请求导航规划器提供一个跳跃步骤。

## 关键输入 / 输出
- 输入：`target`（包含 `Vector2`、`Vector3` 或 Unity 物体的 `VariableField`）、`jumpHeight`、`jumpLength`，以及可选的 `offset`。
- 选项：`targetMode`（`Direct` 或 `PlannedStep`）、规划目标的 `targetMeasurement`、`goal`、`distanceMetric`、`reachDistance` 和 `skipReached`。
- 独立开关：`allowAirborneLaunch`（允许空中起跳）和 `allowAirborneEndpoint`（允许空中终点），默认均关闭；旧资源缺少字段时同样保留地面支撑要求。分别开启后可以从空中起跳、以空中位置为终点；空中终点仍要求观察到下降中的身体实际到达，不能只靠飞行计时完成。
- 输出：无。

## 成功 / 失败语义
- 跳跃执行器完成时成功；启用 `skipReached` 且目标已经满足配置的目标条件时，也会立即成功。
- 目标或跳跃参数无效、导航无法提供可用跳跃，或执行失败时失败。

## 重要限制
- 即使开启两个空中选项，也需要导航运行时、已发布的导航世界、`Rigidbody2D` 和启用的导航碰撞体。Direct 物体目标需要碰撞体；PlannedStep 在目标没有碰撞体时使用 Transform 位置，与 Jump Trace 一致。
- `PlannedStep` 与 `Jump` 的 `Simple` 模式使用同一个单步规划器。身体、目标几何、目标条件、支撑开关和物理参数相同时选择相同的一跳；FixedJump 完成这一跳就结束，Jump 则继续追求最终目标。默认碰撞体测量和零偏移与 Jump Trace 对齐；显式 Transform 测量或偏移会改变目标几何。
- `Direct` 验证通向指定脚底中心终点的轨迹，不搜索候选落点。两种模式都保留世界/区域、碰撞、跳跃能力和单向平台检查。
- 开启 `allowAirborneEndpoint` 后，规划还会尝试把目标下沿中心作为空中终点，并保留现有地面候选；这不会增加自由飞行搜索网格，也不保证任意空中目标都有路径。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
