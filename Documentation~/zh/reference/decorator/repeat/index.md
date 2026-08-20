# `Repeat`

## 用途

按固定次数重复执行一个子节点。

## 关键输入 / 输出

- 输入：`node` (`TreeNode`)、`repeatCount` (`VariableField<int>`)。
- 输出：无。

## 成功 / 失败语义

- 次数为零或负数时，不执行子节点并直接成功。
- 没有有效子节点时失败。
- 子节点第一次失败时，Repeat 立即失败。
- 所有请求的执行都成功后，Repeat 成功。

## 重要限制

- 每次 Repeat 开始时只读取一次次数。
- Repeat 不提供条件终止、Break 或无限重复。
- 需要执行期间改变终止条件时，请使用 `Loop`。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Repeat.cs)
