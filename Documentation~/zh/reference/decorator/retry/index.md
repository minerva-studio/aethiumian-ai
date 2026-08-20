# `Retry`

## 用途

子节点失败后按固定最大次数重试。

## 关键输入 / 输出

- 输入：`node` (`TreeNode`)、`maxAttempts` (`VariableField<int>`)。
- 输出：无。

## 成功 / 失败语义

- 没有有效子节点时失败。
- 最大尝试次数为零时不执行子节点并失败。
- 任意一次子节点成功后，Retry 立即成功。
- 子节点失败时，只要仍有剩余次数就继续尝试。
- 所有允许的尝试都失败后，Retry 失败。

## 重要限制

- 每次 Retry 开始时只读取一次尝试次数。
- `maxAttempts` 包含第一次尝试，并会被钳制为不小于零。
- Retry 不捕获异常，继续使用现有节点和树的错误策略。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Decorators/Retry.cs)
