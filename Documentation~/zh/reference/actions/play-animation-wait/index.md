# `PlayAnimationWait`

## 用途
播放一个动画状态，并等待该状态不再处于当前。

## 关键输入 / 输出
- 输入：`stateName` (`VariableField<string>`), `layer` (`VariableField<int>`)。
- 输出：无。

## 成功 / 失败语义
- 当 条件满足 时成功。
- 当 `Animator` 组件缺失时失败。

## 重要限制
- 更新模式遵循 `Animator` 的更新模式（`AnimatePhysics` 与 `Normal`）。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/PlayAnimationWait.cs)
