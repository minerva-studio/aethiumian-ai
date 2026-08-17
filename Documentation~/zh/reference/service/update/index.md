# `Update`

## 用途
以周期服务动作方式重复执行一个子树。

## 关键输入 / 输出
- 输入：`间隔` (`int`), `forceStopped` (`VariableField<bool>`), `subtreeHead` (`NodeReference`)。
- 输出：无。

## 成功 / 失败语义
- 子树启动并成功完成时成功。
- 子树缺失或返回 `false` 时失败。

## 重要限制
- `forceStopped` 决定是否可覆盖仍在运行的旧例程。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Update.cs)
