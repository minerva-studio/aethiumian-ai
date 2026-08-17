# `Rollback`

## 用途
将当前活动的节点栈回滚到指定节点。

## 关键输入 / 输出
- 输入：`stopAt` (`RawNodeReference`), `yield` (bool)。
- 输出：无。

## 成功 / 失败语义
- 当目标节点存在于当前栈中且栈指针成功回退时返回成功。

## 重要限制
- 在服务宿主模式下，仅回滚当前服务栈。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Flows/Rollback.cs)
