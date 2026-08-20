# `Capture`

## 用途

将子节点的布尔结果写入可写变量，同时原样转发该结果。

## 关键输入 / 输出

- 输入：`result` (`VariableReference<bool>`)。
- 输出：无；仅当 `result` 存在有效引用时写入捕获值。

## 成功 / 失败语义

- 原样返回子节点结果：成功仍为成功，失败仍为失败。
- 当 `result` 没有有效引用时，仍会转发子节点结果，不改变执行结果。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Decorators/Capture.cs)
