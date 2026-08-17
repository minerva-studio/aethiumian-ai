# `Raycast`

## 用途
执行一次 3D 射线检测并输出命中结果。

## 关键输入 / 输出
- 输入：`center`, `direction`, `距离`, `layerMask`。
- 输出：`result` (`RaycastHit`)。

## 成功 / 失败语义
- 根据是否命中碰撞体返回。

## 重要限制
- 固定 3D 射线检测；除图层过滤外无额外命中过滤。

## 源码链接
- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Calls/Raycast.cs)
