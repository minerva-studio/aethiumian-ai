# `GetComponentValue`

## 用途
读取组件上的字段值。

## 关键输入 / 输出
- 输入：`getComponent`, `组件`, `type`。
- 输出：从选定对象读取基础字段值。

## 成功 / 失败语义
- 取决于底层 `getter` 是否可用。

## 重要限制
- 需有效的反射成员与目标组件。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/GetComponentValue.cs)
