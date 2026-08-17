# `Stop`

## 用途
按可选策略减速或停止移动。

## 关键输入 / 输出
- 输入：`idleType`, `speed`, `velocityErrorBound`, `时间`。
- 输出：无。

## 成功 / 失败语义
- 当运动达到停止状态时成功。

## 重要限制
- 需要 `Rigidbody2D`。
- 缺失物理体时立即完成。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Stop.cs)
