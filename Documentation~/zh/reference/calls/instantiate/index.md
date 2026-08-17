# `Instantiate`

## 用途
实例化一个预制体到场景中。

## 关键输入 / 输出
- 输入：`original`, `parentOfObject`, `offsetMode`, `offset`。
- 输出：已实例化的 `result`。

## 成功 / 失败语义
- `Success` 表示实例化成功。
- `失败` 当源预制体无效。

## 重要限制
- 父级与偏移处理仅支持枚举中列出的选项。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Instantiate.cs)
