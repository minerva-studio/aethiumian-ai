# `DestroyComponent`

## 用途
从宿主对象移除一个组件。

## 关键输入 / 输出
- 输入：`componentReference` (`TypeReference<组件>`)。
- 输出：无。

## 成功 / 失败语义
- `Success` 在发送销毁调用后。

## 重要限制
- 缺少目标组件时会使用 `null` 目标类型进行 `Unity` 销毁调用。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/DestroyComponent.cs)
