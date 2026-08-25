# `FunctionAction`

## 用途
把选中的函数作为 `Action` 执行。`FunctionAction` 复用 `FunctionCall` 的函数选择器与接收者选择方式，挂载于 `Action` 生命周期，并可等待跨帧完成。

## 关键输入 / 输出
- 输入：`function` (`FunctionReference`), `targetObject` (`VariableReference`), `parameters` (`List<Parameter>`)、`returnMode` (`ReturnMode`)、`result` (`VariableReference`)。
- 输出：当方法返回可用时，`result` 取得该方法返回值。

## 成功 / 失败语义
- `Default`：正常完成即成功；`NodeProgress.Complete(bool)` 仍保留显式结果。
- `ReturnValue`：按变量到 `bool` 的规则处理 `Task<T>` / `Awaitable<T>` 返回值；无值的 `Task`、`Awaitable` 与 `IEnumerator` 使用默认完成结果。
- `AlwaysSuccess` 与 `AlwaysFailure`：强制映射正常完成。非 Default 模式下若 `NodeProgress` 提前完成会记录 Warning；异常与取消仍遵循既定失败/exception rule。

## 重要限制
- 支持静态调用；实例调用需要有效目标。
- 被调用方法必须满足动作方法校验。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/FunctionAction.cs)
