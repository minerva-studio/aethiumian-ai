# `Restart`

## 用途
重载当前运行中的行为树。

## 关键输入 / 输出
- 输入：无。
- 输出：无。

## 成功 / 失败语义
- 当宿主树成功重载且旧执行栈被替换时成功。

## 重要限制
- 不会向父节点透传直接返回值。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Restart.cs)
