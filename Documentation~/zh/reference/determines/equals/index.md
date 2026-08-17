# `Equals`

## 用途
比较两个值是否相等。

## 关键输入 / 输出
- 输入：`a`, `b`。
- 输出：bool。

## 成功 / 失败语义
- 按类型规则返回 `a == b` 的比较结果。
- 失败通过返回 `false` 表现。

## 重要限制
- 支持内置变量类型比较，并包含 Unity 对象身份比较。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/Equals.cs)
