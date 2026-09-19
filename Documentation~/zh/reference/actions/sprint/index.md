# `Sprint`

## 用途
在有限时长内向 AI 宿主施加配置的力。

## 关键输入 / 输出
- 输入：`duration`、`force` 和 `forceApplication`（`Timed` 或 `Legacy`）。
- 输出：无。

## 成功 / 失败语义
- `Timed` 在施力时段结束后成功；若移动源不再允许移动则失败。
- `Legacy` 在每个固定更新中施力，经过时间大于 `duration` 后成功结束。

## 重要限制
- 宿主需要有 `Rigidbody2D`，控制目标需要实现 `IMovementSource`。
- `Legacy` 保留旧版重复施力行为；`Timed` 使用有时限的施力执行器。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/Sprint.cs)
