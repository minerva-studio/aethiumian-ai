# `WaitForAnimationEnd`

## 用途
等待目标动画状态变化。

## 关键输入 / 输出
- 输入：`animation` (`current` 或 `stageName`), 可选 `stageName` (`VariableField<string>`)。
- 输出：无。

## 成功 / 失败语义
- 当 条件满足 时成功。

## 重要限制
- 需要宿主对象上存在 `Animator`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/WaitForAnimationEnd.cs)
