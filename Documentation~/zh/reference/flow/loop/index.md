# `Loop`

## 用途
按固定次数或条件循环类型重复执行子节点。

## 关键输入 / 输出
- 输入：`loopType`, `loopCount` (for count mode), `condition` (for while modes), `events` (`List<TreeNode>`)。
- 输出：无。

## 成功 / 失败语义
- 迭代期间通常返回成功；在服务模式下若无有效子节点时可失败。

## 重要限制
- 循环中的子节点可能在继续前停顿一帧。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Loop.cs)
