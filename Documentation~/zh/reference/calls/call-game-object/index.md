# `CallGameObject`

## 用途
通过反射调用 `GameObject` 或组件上的方法。

## 关键输入 / 输出
- 输入：`getGameObject`, `pointingGameObject`, `methodName`, `parameters`。
- 输出：可选 `result`。

## 成功 / 失败语义
- 布尔方法按返回值映射；非布尔方法通常返回成功。
- `失败` 当方法无法解析或调用。

## 重要限制
- 方法签名必须匹配输入参数。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/CallGameObject.cs)
