# `ResultChanged`

## 用途
监控子节点，并在两次执行结果变化时返回成功。

## 关键输入 / 输出
- 输入：`subtreeHead` (`NodeReference`)。
- 输出：无。

## 成功 / 失败语义
- 结果发生变化时成功；当无子节点或结果重复时失败。

## 重要限制
- 首次返回仅用于初始化比较状态，不计入结果变化。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/ResultChanged.cs)
