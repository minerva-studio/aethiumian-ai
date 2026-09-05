# `Timeout`

## 用途

在经过配置时间后中断宿主执行。

## 关键输入 / 输出

- 输入：`time` (`VariableField<float>`)、`result` (`Failed`/`Success`)。
- 输出：无。

## 成功 / 失败语义

- 到达 deadline 后按配置的 `result` 中断宿主。
- 使用所属根树的 `timeSettings`，分别选择 Game/AI 与 Scaled/Unscaled，零值默认是
  Game + Scaled。

## 重要限制

- 保留现有 Service 检查调度：Unscaled 可以让时间在 `timeScale = 0` 时推进，
  但不会保证 AI Pause 或驱动停止时仍执行检查。
- 树内 Timeout 使用根树共享计时对象。Pause 不会冻结外部异步函数，但会推迟
  Timeout 的 Service 检查，直到树驱动恢复。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timeout.cs)
