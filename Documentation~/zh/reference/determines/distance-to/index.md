# `DistanceTo`

## 用途
按可配置度量方式测量到目标游戏对象的距离。

## 关键输入 / 输出
- 输入：`distanceType`, `对象` (`VariableReference<GameObject>`)。
- 输出：`float` 距离。

## 成功 / 失败语义
- 目标缺失时返回 `float.PositiveInfinity`。
- 否则按选定 metric 返回距离值。

## 重要限制
- 可选指标包括 Euclidean、Manhattan、Chebyshev。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/DistanceTo.cs)
