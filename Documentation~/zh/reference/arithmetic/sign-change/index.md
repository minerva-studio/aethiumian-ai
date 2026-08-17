# `SignChange`

## 用途
检查值相对于阈值的符号变化，并写入布尔标志位。

## 关键输入 / 输出
- 输入：`值`, `bound`, `determine`, `baseValue`, `change`。
- 输出：`change`（`bool`）。

## 成功 / 失败语义
- 节点执行成功。

## 重要限制
- 未检测到变号时返回失败。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/SignChange.cs)
