# `DirectionTo`

## 用途
计算从源到目标的方向向量。

## 关键输入 / 输出
- 输入：`overrideCenter`, `center`, `目标`。
- 输出：`result` 方向向量。

## 成功 / 失败语义
- 目标存在且源上下文有效时成功。

## 重要限制
- 如果 `overrideCenter` 为 `true`，必须设置 `center`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/DirectionTo.cs)
