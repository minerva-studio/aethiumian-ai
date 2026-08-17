# `IsPlayingAnimation`

## 用途
检查当前动画器状态是否与配置名称一致。

## 关键输入 / 输出
- 输入：`stageName`。
- 输出：bool。

## 成功 / 失败语义
- 当当前动画器处于 `stageName` 状态时返回 `true`。

## 重要限制
- 宿主对象需要 `Animator` 组件。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/IsPlayingAnimation.cs)
