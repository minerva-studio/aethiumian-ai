# `Cooldown`

## 用途

仅在共享计时器就绪时准入子节点，并在子节点成功后启动计时器。

## 关键输入 / 输出

- 输入：`node`（子节点）、`duration`（`VariableField<float>`）、`timer`（`VariableReference<float>`）。
- 输出：子节点结果；计时器活动期间返回失败。

## 成功 / 失败语义

- 子节点成功完成后启动计时器并返回成功。
- 子节点失败或被中断时不会消耗计时器。
- 计时器就绪时，空子节点视为立即成功。

## 重要限制

- `timer` 必须解析为本地 `TimerVariable`。
- 只有子节点成功或空子节点准入时才读取持续时间。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Cooldown.cs)
