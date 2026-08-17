# `IsTypeOf`

## 用途
检查值是否具有精确的运行时类型。

## 关键输入 / 输出
- 输入：`变量`, `type`。
- 输出：bool。

## 成功 / 失败语义
- 当 `variable.Value.GetType() == type` 时返回 `true`。

## 重要限制
- 需要 `type` 引用和非空的输入变量。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsTypeOf.cs)
