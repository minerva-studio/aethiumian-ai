# `TypeOf`

## 用途
读取变量值的运行时 `.NET` 类型。

## 关键输入 / 输出
- 输入：`变量`, `result`。
- 输出：`bool` 状态和 `result` 类型对象。

## 成功 / 失败语义
- 成功状态为值比较结果（`变量 != null`）。

## 重要限制
- 输入值缺失会抛出节点异常。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/TypeOf.cs)
