# `RaycastDistance`

## 用途
测量从起点沿指定方向到首次射线检测命中的距离。

## 关键输入 / 输出
- 输入：`physicsMode`, `center`, `direction`, `distance`, `layerMask`。
- 输出：`float` 距离（若未命中则返回 `float.MaxValue`）。

## 成功 / 失败语义
- 命中时返回有限距离，否则返回 `float.MaxValue`。

## 重要限制
- 同时支持 `Physics2D` 与 `Physics3D`。
- 位置参数无效时可能在节点校验阶段失败。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/RaycastDistance.cs)
