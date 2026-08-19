# `Sequence`

## 用途
按顺序执行子节点，并使用短路 AND 语义。

## 关键输入 / 输出
- 输入：`events` (`NodeReference[]`)。
- 输出：一个成功或失败结果。

## 成功 / 失败语义
- 任一子节点失败时立即返回失败。
- 所有子节点成功时返回成功。
- 空 Sequence 返回成功。

## 重要限制
- 这是从旧版“全执行 OR 聚合”到“短路 AND”的有意行为变更；现有资产不迁移，直接采用新语义。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Sequence.cs)
