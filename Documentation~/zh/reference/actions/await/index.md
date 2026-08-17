# `Await`

## 用途
等待引用的异步任务执行完成后再返回。

## 关键输入 / 输出
- 输入：`task` (`VariableReference<Task>`)。
- 输出：`result` (`VariableReference`) 当任务返回值产生时。

## 成功 / 失败语义
- 当 条件满足 时成功。
- 当节点无法解析或执行该异步任务时失败。

## 重要限制
- 仅支持有效的任务引用。
- 若未提供有效任务，运行时将无法生成有意义的返回。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Await.cs)
