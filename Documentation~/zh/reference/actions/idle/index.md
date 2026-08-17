# `Idle`

## 用途
让当前实体减速到零速；若未使用该节点，实体通常只能通过 `Physics2D` 的摩擦力或碰撞自身停止。

## 关键输入 / 输出
- 输入：`时间`, `strength` (`VariableField<float>`)。
- 输出：无。

## 成功 / 失败语义
- 当 条件满足 时成功。

## 重要限制
- 动作更新基于插值执行。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Idle.cs)
