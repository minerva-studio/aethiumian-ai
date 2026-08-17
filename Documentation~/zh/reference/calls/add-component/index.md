# `AddComponent`

## 用途
向选定的 `GameObject` 按类型添加一个组件。

## 关键输入 / 输出
- 输入：`component` (`TypeReference<Component>`), `targetGameObject` (`ParentMode` = `underSelf` 或 `underParent`)。
- 输出：无。

## 成功 / 失败语义
- `Success` 当组件添加成功。
- `失败` 当目标无效。

## 重要限制
- `underParent` 需要 `transform.parent`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/AddComponent.cs)
