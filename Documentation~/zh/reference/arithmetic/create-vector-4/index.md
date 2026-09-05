# `CreateVector4`

## 用途

由数值或向量分量构建 `Vector4`。

## 关键输入 / 输出

- 输入：`x`、`y`、`z`、`w` 及其分量选择器。
- 输出：`vector`（`VariableReference<Vector4>`）。

## 成功 / 失败语义

- 输出绑定为 `Vector4` 引用且所有分量都能读写时返回成功。
- 输出绑定或任一输入分量无效时返回失败。

## 重要限制

- 输入分量通过作者配置的 `VectorLane` 选择解析。
- 转换异常通过节点标准算术错误处理报告。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Arithmetics/CreateVector4.cs)
