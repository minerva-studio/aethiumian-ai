# `FunctionAction`

## 用途
把选中的函数作为 `Action` 执行。`FunctionAction` 复用 `FunctionCall` 的函数选择器与接收者选择方式，挂载于 `Action` 生命周期，并可等待跨帧完成。

## 关键输入 / 输出
- 输入：`function` (`FunctionReference`), `targetObject` (`VariableReference`), `parameters` (`List<Parameter>`), `result` (`VariableReference`)。
- 输出：当方法返回可用时，`result` 取得该方法返回值。

## 成功 / 失败语义
- 成功和失败由返回布尔值决定：`true` 成功，`false` 失败。
- 对 `Task` / `IEnumerator` / `Awaitable` 方法会等待其执行完成。

## 重要限制
- 支持静态调用；实例调用需要有效目标。
- 被调用方法必须满足动作方法校验。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/FunctionAction.cs)
