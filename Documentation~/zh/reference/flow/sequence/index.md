# `Sequence`

## 用途
按顺序执行所有子节点，不因某个子节点返回 `false` 而中止。

## 关键输入 / 输出
- 输入：`events` (`List<TreeNode>`)。
- 输出：无。

## 成功 / 失败语义
- 至少有一个子节点成功时返回成功。

## 重要限制
- 子节点失败不会导致提前停止。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Sequence.cs)
