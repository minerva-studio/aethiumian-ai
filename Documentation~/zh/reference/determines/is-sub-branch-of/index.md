# `IsSubBranchOf`

## 用途
判断当前节点是否位于指定分支引用中（本节点或其子节点）。

## 关键输入 / 输出
- 输入：`root` (`RawNodeReference`)。
- 输出：bool。

## 成功 / 失败语义
- 当当前阶段节点与 `root` 相同，或位于 `root` 的后代节点时返回 `true`。

## 重要限制
- 无效引用会按 `false` 处理。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsSubBranchOf.cs)
