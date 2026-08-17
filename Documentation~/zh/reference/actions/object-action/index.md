# `ObjectAction`

## 用途
旧的对象 `Action` 节点，保留兼容旧资产。新的跨帧/异步方法动作请优先使用 `FunctionAction`。

## 关键输入 / 输出
- 输入：`对象` (`VariableReference`), `type` (`GenericTypeReference`), `methodName`, `parameters`, `actionCallTime`, `时长`, `count`, `endType`, `result`。
- 输出：`result` (`VariableReference`) 的调用返回值可用时。

## 成功 / 失败语义
- 由动作完成规则与调用方法返回值共同决定。
- 当方法解析或调用无效时失败。

## 重要限制
- 反射调用要求签名与目标类型有效。
- 仅当动作方法规则满足时才按动作处理。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/ObjectAction.cs)
