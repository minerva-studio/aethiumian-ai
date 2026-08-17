# `Break`

## 用途
终止一个节点的执行

## 关键输入 / 输出
- 输入：`returnTo` (`ReturnType`), `condition` (`TreeNode`)。
- 输出：无。

## 成功 / 失败语义
- 条件分支成功且服务中断生效后始终返回成功。

## 重要限制
- 条件分支返回值通过宿主服务上下文评估。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Break.cs)
