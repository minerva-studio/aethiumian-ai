# `ForEach`

## 用途
对可枚举集合中的每个元素执行一次节点。

## 关键输入 / 输出
- 输入：`enumerable` (`VariableReference`), `item` (`VariableReference`), `event` (`NodeReference`)。
- 输出：无。

## 成功 / 失败语义
- 遍历完成时返回成功。

## 重要限制
- `enumerable` 必须非空且实现 `IEnumerable`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/ForEach.cs)
