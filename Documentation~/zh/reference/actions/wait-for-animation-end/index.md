# `WaitForAnimationEnd`

## 用途
等待 Animator 状态自然结束或被打断。

## 关键输入 / 输出
- 输入：`animation` (`current` 或 `stageName`), 可选 `stageName` (`VariableField<string>`)。
- `stageName` 模式要求输入 Layer 0 的完整状态路径，例如 `Base Layer.Attack` 或 `Base Layer.Combat.Attack`。
- 输出：无。

## 成功 / 失败语义
- `current` 模式从动作开始时跟踪 Layer 0 当前状态；`stageName` 模式会先等待配置状态成为当前状态，再开始跟踪。
- 被跟踪的非循环状态在自然播放完毕（归一化时间至少为 `1`）、Layer 0 开始状态转换或当前状态改变时成功。
- 循环状态不会自然结束，会一直等待到被状态转换或状态改变打断；自转换也算打断。
- 宿主缺少 Animator 或 Runtime Animator Controller、`current` 模式下 Layer 0 没有有效当前状态，或配置路径为空/不是 Layer 0 状态时立即失败。

## 重要限制
- 宿主对象需要有带 Runtime Animator Controller 的 Animator。
- `Fixed` 更新模式在 `FixedUpdate` 采样；`Normal` 与 `UnscaledTime` 在 `Update` 采样。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/WaitForAnimationEnd.cs)
