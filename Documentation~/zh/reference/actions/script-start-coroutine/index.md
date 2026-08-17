# `ScriptStartCoroutine`

## 用途
启动 `AI` 脚本中的一个协程方法。

## 关键输入 / 输出
- 输入：`methodName`, `afterExecuteAction`。
- 输出：无。

## 成功 / 失败语义
- 当模式为 `continue` 时立即返回；当模式为 `waitUntilEnd` 时在协程完成后返回。

## 重要限制
- 目标方法必须是有效的协程入口。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/ScriptStartCoroutine.cs)
