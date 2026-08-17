# `AddForce`

## 用途
向当前对象施加二维力。

## 关键输入 / 输出
- 输入：`强制` (`VariableField<Vector2>`)。
- 输出：无。

## 成功 / 失败语义
- `Success` 当力施加成功。
- `失败` 当未提供 `Rigidbody2D`。

## 重要限制
- 需要宿主对象具备 `Rigidbody2D`。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/AddForce.cs)
