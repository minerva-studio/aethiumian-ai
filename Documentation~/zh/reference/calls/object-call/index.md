# `ObjectCall`

## 用途
执行脚本中的指定方法

## 关键输入 / 输出
- 输入：`对象`, `type`, `parameters`。
- 输出：可选 `result`。

## 成功 / 失败语义
- 布尔方法返回布尔值结果；否则按成功返回。
- `失败` 当方法解析或调用抛出错误。

## 重要限制
- 目标类型上需存在可反射的实例方法。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/ObjectCall.cs)
