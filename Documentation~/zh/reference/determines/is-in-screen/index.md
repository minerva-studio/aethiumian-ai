# `IsInScreen`

## 用途
判断世界坐标是否位于主摄像机屏幕可见范围内。

## 关键输入 / 输出
- 输入：`position` (`Vector2/Vector3/unityObject`)。
- 输出：bool。

## 成功 / 失败语义
- 当投影点位于 `[0, Screen]` 区间内时返回 `true`。

## 重要限制
- 使用 `Camera.main`；若当前不存在主摄像机则返回失败。
- 结果受当前启用摄像机投影模式影响。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsInScreen.cs)
