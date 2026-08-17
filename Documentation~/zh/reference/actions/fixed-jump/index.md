# `FixedJump`

## 用途
将当前对象按固定高度抛物线向目标跳跃。

## 关键输入 / 输出
- 输入：`jumpHeight` (`VariableField<float>`), `jumpDuration` (`VariableField<float>`), `目标` (`VariableField<Vector2|Vector3|UnityObject>`)。
- 输出：无。

## 成功 / 失败语义
- 在跳跃时长结束后，节点到达计算终点时成功。
- 当目标为空或无效时失败。

## 重要限制
- 宿主对象需要 `Rigidbody2D`。
- 使用固定步进时间进行移动。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
