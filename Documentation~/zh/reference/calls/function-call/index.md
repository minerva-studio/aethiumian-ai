# `FunctionCall`

## 用途
调用一次选定函数。

## 关键输入 / 输出
- 输入：`function`, `targetObject`, `parameters`。
- 输出：可选 `result`。

## 成功 / 失败语义
- 布尔方法按返回值映射。
- `失败` 若查找或调用抛出异常。

## 重要限制
- 静态方法以 `null` 目标调用。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Functions/FunctionCall.cs)
