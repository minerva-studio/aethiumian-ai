# `GetComponent`

## 用途
从自身、父节点或子节点读取组件实例。

## 关键输入 / 输出
- 输入：`getMode`, `getMultiple`, `includeInactive`, `type`。
- 输出：`result`（组件或组件数组）。

## 成功 / 失败语义
- `Success` 当请求数据存在。
- `失败` 当未匹配到结果。

## 重要限制
- 搜索范围受 `getMode` 影响，可能包含未激活对象。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/GetComponent.cs)
