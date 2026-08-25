# `FunctionCall`

## 用途
调用一次选定函数。

## 关键输入 / 输出
- 输入：`function`, `targetObject`, `parameters`、`returnMode`（`Default`、`ReturnValue`、`AlwaysSuccess` 或 `AlwaysFailure`）。
- 输出：可选 `result`。

## 成功 / 失败语义
- `Default`：正常调用完成即成功。
- `ReturnValue`：按项目现有变量到 `bool` 的隐式转换映射返回值；不支持转换的对象按非空为真。
- `AlwaysSuccess` 与 `AlwaysFailure`：强制映射正常完成结果。
- 调用异常遵循树的 exception rule；`result` 仍独立保存返回值。

## 重要限制
- 静态方法以 `null` 目标调用。
- `FunctionCall` 不会等待 `Task`、`Awaitable` 或 Coroutine；需要读取完成值时请使用 `FunctionAction`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Functions/FunctionCall.cs)
