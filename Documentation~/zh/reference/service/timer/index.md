# `Timer`

## 用途
每个服务帧更新并递减一个 `float` 变量。

## 关键输入 / 输出
- 输入：`updatingVariable` (`VariableReference<float>`), `timing` (`Timing`)。
- 输出：无。

## 成功 / 失败语义
- 作为服务节点存活期间返回成功。

## 重要限制
- 时间模式会影响是否采用固定更新时间频率。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Timer.cs)
