# `PlayAnimation`

## 用途
立即播放一个动画状态。

## 关键输入 / 输出
- 输入：`stateName`, `layer`。
- 输出：无。

## 成功 / 失败语义
- 播放请求发起后返回 `Success`。
- `失败` 若 `Animator` 组件缺失。

## 重要限制
- 此节点无完成等待逻辑。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/PlayAnimation.cs)
