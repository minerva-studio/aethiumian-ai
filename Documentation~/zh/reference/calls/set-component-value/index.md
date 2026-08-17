# `SetComponentValue`

## 用途
设置附加组件的字段或属性。

## 关键输入 / 输出
- 输入：`getComponent`, `组件`, `type`, `setter` 描述符。
- 输出：修改宿主/目标组件。

## 成功 / 失败语义
- `Success` 当基础 `setter` 成功。

## 重要限制
- `getComponent=false` 时需要有效对象引用。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/SetComponentValue.cs)
