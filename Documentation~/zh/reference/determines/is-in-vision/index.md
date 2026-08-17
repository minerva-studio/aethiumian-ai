# `IsInVision`

## 用途
判断目标是否对当前实体可见。

## 关键输入 / 输出
- 输入：`目标`, `offset`, `maxDistance`, `blockingLayers`。
- 输出：bool。

## 成功 / 失败语义
- 当目标在允许距离内且未被阻挡层挡住时返回 `true`。

## 重要限制
- 使用 `Collider2D` 与 `Physics2D.Raycast`。
- 距离回退逻辑在可用时会使用碰撞体距离。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsInVision.cs)
