# `Wait`

## 用途
等待设定的时间或帧数，再继续执行。

## 关键输入 / 输出
- 输入：`mode` (`realTime` or `frame`), `时间` (`VariableField<float>`)。
- 输出：无。

## 成功 / 失败语义
- 超时后始终返回成功。

## 重要限制
- 帧模式行为取决于 `FixedUpdate` 与普通更新调度。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Wait.cs)
