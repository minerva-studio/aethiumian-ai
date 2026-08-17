# `Subtree`

## 用途
将引用的行为树数据资源作为嵌套树执行。

## 关键输入 / 输出
- 输入：`behaviourTreeData`, `variableTable` 映射。
- 输出：继承自子树的结果。

## 成功 / 失败语义
- 成功/失败继承自子树最终结果。
- 当子树无法初始化或执行时失败。

## 重要限制
- 父子变量仅通过映射表连接。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Subtree.cs)
