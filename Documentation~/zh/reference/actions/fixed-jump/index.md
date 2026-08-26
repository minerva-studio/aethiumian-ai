# `FixedJump`

## 用途
将当前对象按固定高度抛物线向目标跳跃。

## 关键输入 / 输出
- 输入：`jumpHeight` (`VariableField<float>`)、`speed` (`VariableField<float>`)、`speedModifier` (`VariableField<float>`)、`target` (`VariableField<Vector2|Vector3|UnityObject>`)。
- 输出：无。

最终水平速度为 `speed * speedModifier`。飞行时间由弹道轨迹求解得出，不再单独配置。

## 成功 / 失败语义
- 求解出的弹道轨迹到达计算终点时成功。
- 当目标为空或无效时失败。

## 重要限制
- 控制目标必须实现 `IMovementSource`。
- AI 宿主 GameObject 需要 `Rigidbody2D` 与 `Collider2D`。
- 使用固定步进时间进行移动。
- 轨迹要求竖直向下的重力、正数速度与跳跃高度、正数重力缩放，以及为零的线性阻尼。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Actions/Movement/FixedJump.cs)
