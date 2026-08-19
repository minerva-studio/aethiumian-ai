# `Aggregate`

## 用途

按顺序执行全部子节点，并将所有子节点结果聚合为一个布尔结果。

## 关键输入 / 输出

- 输入：`events` (`NodeReference[]`) 与 `resultMode`。
- 输出：一个聚合后的成功或失败结果。

## 成功 / 失败语义

- `All` 仅在全部子节点成功时返回成功；空 Aggregate 返回成功。
- `Any` 在至少一个子节点成功时返回成功；空 Aggregate 返回失败。
- `True` 执行全部子节点后固定返回成功；空 Aggregate 返回成功。
- `False` 执行全部子节点后固定返回失败；空 Aggregate 返回失败。
- 子节点正常返回成功或失败时不会短路。
- Error、异常和中断由行为树继续传播，不转换成普通失败。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Aggregate.cs)
