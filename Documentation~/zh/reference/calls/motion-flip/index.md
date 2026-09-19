# `MotionFlip`

## 用途
根据宿主 `Rigidbody2D` 的水平速度更新本地 `SpriteRenderer` 的水平翻转状态。

## 关键输入 / 输出
- 输入：宿主上的 `Rigidbody2D` 和 `SpriteRenderer` 组件。
- 输出：水平速度超过节点阈值时更新 `SpriteRenderer.flipX`。

## 成功 / 失败语义
- 两个必需组件都存在时成功；向左移动会设置 `flipX`，向右移动会清除它。
- 缺少任一必需组件时失败。

## 重要限制
- 水平速度小于或等于阈值时保留当前翻转状态。
- 节点只修改本地渲染器，不会旋转或移动物体。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/MotionFlip.cs)
