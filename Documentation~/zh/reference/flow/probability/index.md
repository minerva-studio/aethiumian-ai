# `Probability`

## 用途
按权重随机选择一个子节点并执行。

## 关键输入 / 输出
- 输入：`events` (`EventWeight` list containing 节点+weight)。
- 输出：无。

## 成功 / 失败语义
- 返回已执行子节点的结果。

## 重要限制
- 零权重或缺失权重可能导致某些合法候选被跳过（取决于解析器行为）。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Probability.cs)
