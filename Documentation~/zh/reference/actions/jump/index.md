# `Jump`

## 用途
沿导航规划器提供的抛物线跳跃步骤，朝配置的目标移动实体。

## 关键输入 / 输出
- 通用移动输入：`type`、`path`，以及对应行为选择的目标（`tracing`、`destination` 或漫游设置）；适用时还包括 `goal`、`distanceMetric` 和 `reachDistance`。
- 跳跃参数：`jumpHeight`、`jumpLength`、`jumpInterval` 和 `speedModifier`（用于缩放两次起跳之间的间隔）。
- `allowAirborneLaunch` 和 `allowAirborneEndpoint` 分别允许空中起跳和空中终点，默认均关闭，保留旧资源的地面支撑要求；与 FixedJump 共用求解器及执行器策略。
- 输出：无。

## 成功 / 失败语义
- 到达配置的移动目标且垂直速度非正时成功；未开启 `allowAirborneEndpoint` 时还必须具有实际地面支撑。
- 目标或路线无效，或规划的跳跃无法执行时失败。

## 重要限制
- 需要导航运行时、`Rigidbody2D`、启用的导航碰撞体，以及实现 `IMovementSource` 的控制目标。
- 未开启 `allowAirborneLaunch` 时，初始规划等待实际地面支撑，每次起跳还要求兼容的导航支撑面；开启后允许从当前空中姿态施加起跳冲量，并跳过起跳支撑查询。
- 开启 `allowAirborneEndpoint` 后，规划可把空中目标作为终点，但执行仍须观察到实际到达。现有地面候选仍保留；Simple 每次选择一个更接近目标的动作，不保证完整路径。
- 每次起跳遵循配置的间隔。等价输入下，FixedJump PlannedStep 使用相同的下一步规划，但在第一次动作成功后就结束。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Jump.cs)
