# `Interrupt`

## 用途
当条件为 `true` 时中断宿主节点。

## 关键输入 / 输出
- 输入：`condition` (`TreeNode`), `result` (`ReturnResult`)。
- 输出：无。

## 成功 / 失败语义
- 中断请求提交后，始终返回成功。

## 重要限制
- 若条件为布尔类型，直接读取布尔值可能绕过分支执行路径。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Interrupt.cs)
