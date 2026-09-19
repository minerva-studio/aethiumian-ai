# `Sprint`

## 用途

在有时限的 movement-state 窗口内向 AI 宿主施力，并通过 `IMovementSource` 报告 `Sprinting` / `Idle`。

## 输入

- `force`：可绑定变量的 `Vector2` 施力值。
- `duration`：movement-state 窗口时长；`Repeated` 模式下也限制施力时长。
- `forceApplication`：`Once` 或 `Repeated`，默认 `Once`。

## 施力语义

- `Once`：动作开始时使用 `ForceMode2D.Force` 施力一次；动作仍运行到 `duration` 结束，以便整个窗口报告为 `Sprinting`。
- `Repeated`：在有效 FixedUpdate 中使用 `ForceMode2D.Force` 重复施力，直到达到 `duration`。
- `duration` 为零时，`Once` 仍施力一次但不报告一个零时长的 `Sprinting` 窗口；`Repeated` 不施力。

## 生命周期

- 动作开始时若 movement source 不允许移动，则不施力并失败。
- 运行期间若 movement source 不再允许移动，则停止施力、报告 `Idle` 并失败。
- 正常完成或动作销毁时报告 `Idle`。
- 宿主需要有 `Rigidbody2D`，控制目标需要实现 `IMovementSource`；力值必须有限，时长必须为有限非负值。
