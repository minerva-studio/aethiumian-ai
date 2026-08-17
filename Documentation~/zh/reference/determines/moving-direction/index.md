# `MovingDirection`

## 用途
输出移动方向向量。

## 关键输入 / 输出
- 输入：`usePhysics2D`。
- 输出：`Vector2` direction。

## 成功 / 失败语义
- `usePhysics2D=true` 时使用 `Rigidbody2D.linearVelocity` / `velocity`；否则使用 `Transform` 位移增量。

## 重要限制
- 当 `usePhysics2D=true` 且缺少 `Rigidbody2D` 时，会进入空速度路径。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/MovingDirection.cs)
