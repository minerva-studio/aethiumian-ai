# `IsNull`

## 用途
检查变量是否为空。

## 关键输入 / 输出
- 输入：`变量`。
- 输出：bool。

## 成功 / 失败语义
- 当值为 `Unity` 空或 CLR 空时返回 `true`。

## 重要限制
- 为了获得确定性行为，需要提供有效的变量引用。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsNull.cs)
