# `CallStatic`

## 用途
通过反射执行静态方法调用。

## 关键输入 / 输出
- 输入：`type`, `methodName`, `parameters`。
- 输出：可选 `result`。

## 成功 / 失败语义
- 布尔返回值按布尔结果映射。
- `失败` 如果静态方法无法解析。

## 重要限制
- 公开静态方法必须参数签名匹配。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/CallStatic.cs)
