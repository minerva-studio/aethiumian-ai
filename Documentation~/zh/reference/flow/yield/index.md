# `Yield`

## 用途
让节点执行暂停一帧后再继续。

## 关键输入 / 输出
- 输入：无。
- 输出：无。

## 成功 / 失败语义
- 首次执行时让出一帧；第二次执行时返回成功。

## 重要限制
- 此节点不会执行任何有状态的子节点。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Yield.cs)
