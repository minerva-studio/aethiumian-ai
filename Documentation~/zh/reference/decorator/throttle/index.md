# `Throttle`

## 用途

仅在共享计时器就绪时准入子节点，并在子节点运行前启动计时器。

## 关键输入 / 输出

- 输入：`node`（子节点）、`duration`（`VariableField<float>`）、`timer`（`VariableReference<float>`）。
- 输出：子节点结果；计时器活动期间返回失败。

## 成功 / 失败语义

- 准入时在子节点执行前启动计时器。
- 子节点失败或被中断时，已消耗的计时器仍保持活动。
- 允许准入时，空子节点返回成功。

## 重要限制

- `timer` 必须解析为本地 `TimerVariable`。
- 只有新的执行准入时才读取持续时间。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Throttle.cs)
