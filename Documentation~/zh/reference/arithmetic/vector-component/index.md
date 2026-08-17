# `VectorComponent`

## 用途
将向量拆解为分量值。

## 关键输入 / 输出
- 输入：`vector`。
- 输出：`x`, `y`, `z` 变量。

## 成功 / 失败语义
- 输入为向量时成功。

## 重要限制
- 当前实现会把 `x` 值写入全部输出槽位；在用于界面工具来源时请先验证。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/VectorComponent.cs)
