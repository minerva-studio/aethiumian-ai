# `Parallel`

## 用途
并发运行多个有效子分支，并按指定模式等待其完成。

## 关键输入 / 输出
- 输入：`events` (`NodeReference[]`) and `mode` (`WaitAll` or `WaitAny`)。
- 输出：无。

## 成功 / 失败语义
- 子分支列表为空时立即成功；否则在选定完成条件满足前持续挂起。
- 已完成分支默认返回成功，除非任一分支栈上报异常。

## 重要限制
- 缺失引用和重复引用会被跳过。
- 每个计划执行的分支使用独立运行时调用栈；停止 `Parallel` 会结束这些栈。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Parallel.cs)
