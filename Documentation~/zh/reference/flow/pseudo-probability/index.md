# `PseudoProbability`

## 用途
按带可选防重复偏置的权重概率选择一个子节点。

## 关键输入 / 输出
- 输入：`events` (`EventWeight` list), `maxConsecutiveBranch`, `randomSourceOverride`。
- 输出：无。

## 成功 / 失败语义
- 返回被选中子节点的执行结果，并记录是否立即重复命中同一分支。

## 重要限制
- 仅当 `maxConsecutiveBranch` 为正数时才应用偏置逻辑。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/PseudoProbability.cs)
