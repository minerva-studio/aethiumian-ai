# `Decision`

## 用途
按顺序执行子节点，直到任意一个返回 `true` 时结束。

## 关键输入 / 输出
- 输入：`events` (`List<TreeNode>`)。
- 输出：无。

## 成功 / 失败语义
- 任意一个子节点返回 true 时成功。
- 全部子节点返回 false 时失败。

## 重要限制
- 执行顺序遵循编辑顺序。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Decision.cs)
