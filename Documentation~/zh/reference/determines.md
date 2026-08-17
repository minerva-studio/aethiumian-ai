# 判断节点

## 扩展基类（不计入公开目录）

- `Determine`：布尔型判断基类。
- `ComparableDetermine<T>`：支持获取当前值并与 `expect` 比较。

## 分类节点

<a id="behaviour-tree-stage-name"></a>
### [`BehaviourTreeStageName`](determines/behaviour-tree-stage-name/index.md)
- 用途：执行该节点定义的核心行为。

<a id="current-node-name"></a>
### [`CurrentNodeName`](determines/current-node-name/index.md)
- 用途：读取当前执行节点名。

<a id="distance-to"></a>
### [`DistanceTo`](determines/distance-to/index.md)
- 用途：计算实体与目标对象之间距离。

<a id="equals"></a>
### [`Equals`](determines/equals/index.md)
- 用途：比较两个值是否相等。

<a id="is-component"></a>
### [`IsComponent`](determines/is-component/index.md)
- 用途：执行该节点定义的核心行为。

<a id="is-component-or-game-object"></a>
### [`IsComponentOrGameObject`](determines/is-component-or-game-object/index.md)
- 用途：校验变量是否为 组件 或 参数。

<a id="is-game-object"></a>
### [`IsGameObject`](determines/is-game-object/index.md)
- 用途：执行该节点定义的核心行为。

<a id="is-in-screen"></a>
### [`IsInScreen`](determines/is-in-screen/index.md)
- 用途：检测世界坐标是否在主摄像机屏幕内。

<a id="is-in-vision"></a>
### [`IsInVision`](determines/is-in-vision/index.md)
- 用途：检测目标是否在可见范围内且未被阻挡。

<a id="is-null"></a>
### [`IsNull`](determines/is-null/index.md)
- 用途：检查变量是否为空。

<a id="is-playing-animation"></a>
### [`IsPlayingAnimation`](determines/is-playing-animation/index.md)
- 用途：执行该节点定义的核心行为。

<a id="is-sub-branch-of"></a>
### [`IsSubBranchOf`](determines/is-sub-branch-of/index.md)
- 用途：判断当前节点是否在指定分支内。

<a id="is-type-of"></a>
### [`IsTypeOf`](determines/is-type-of/index.md)
- 用途：判断值的运行时类型是否与配置类型一致。

<a id="moving-direction"></a>
### [`MovingDirection`](determines/moving-direction/index.md)
- 用途：输出移动方向向量。

<a id="name-of-game-object"></a>
### [`NameOfGameObject`](determines/name-of-game-object/index.md)
- 用途：返回当前 AI 对象名称。

<a id="position"></a>
### [`Position`](determines/position/index.md)
- 用途：读取宿主 AI 游戏对象的世界坐标。

<a id="raycast-distance"></a>
### [`RaycastDistance`](determines/raycast-distance/index.md)
- 用途：执行 2D/3D 射线并返回命中距离。

