# `CreateVector3`

## 用途
由数值分量构建 `Vector3`。

## 关键输入 / 输出
- 输入：`x`, `y`, `z`。
- 输出：`vector` (`VariableReference<Vector3>`)。

## 成功 / 失败语义
- 输出向量引用有效时预期成功。

## 重要限制
- 当前源码在写入值后返回失败状态；请以源码为准确认设计预期。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/CreateVector3.cs)
