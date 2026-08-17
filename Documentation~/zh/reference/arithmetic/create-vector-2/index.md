# `CreateVector2`

## 用途
由数值分量构建 `Vector2`。

## 关键输入 / 输出
- 输入：`x`, `y`。
- 输出：`vector` (`VariableReference<Vector2>`)。

## 成功 / 失败语义
- 输出向量引用有效时成功。

## 重要限制
- 缺失分量时默认使用 `0`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/CreateVector2.cs)
