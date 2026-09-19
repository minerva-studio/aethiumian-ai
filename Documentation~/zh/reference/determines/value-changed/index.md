# `ValueChanged`

## 用途
检测被引用变量的当前值是否与上一次观察到的值不同。

## 关键输入 / 输出
- 输入：`value`（`VariableReference`），必须能够解析出值。
- 输出：布尔判断结果。

## 成功 / 失败语义
- 第一次观察时保存基准值并返回 `false`。之后只有在捕获到的值发生变化时返回一次 `true`，随后更新基准值。
- 未设置的变量引用会使节点无效。

## 重要限制
- 比较对象是紧邻的上一次观察值，而不是行为树启动时的值。
- 上一次值快照属于运行时状态，并会在节点初始化时重置。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Determines/ValueChanged.cs)
