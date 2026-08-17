# 动作节点

## 扩展基类（不计入公开目录）

- `Action`：所有动作节点的基类，负责动作节点的执行生命周期。
- `ObjectActionBase`：`ObjectAction` 的公共逻辑基类，提供 `methodName`、参数、`endType` 与 `actionCallTime`。
- `Movement`：运动类抽象基类，在公开目录中不作为独立节点列出。

## 分类节点

<a id="await"></a>
### [`Await`](actions/await/index.md)
- 用途：执行该节点定义的核心行为。

<a id="fixed-jump"></a>
### [`FixedJump`](actions/fixed-jump/index.md)
- 用途：按固定抛物线参数向目标位置施加位移。

<a id="function-action"></a>
### [`FunctionAction`](actions/function-action/index.md)
- 用途：执行该节点定义的核心行为。

<a id="idle"></a>
### [`Idle`](actions/idle/index.md)
- 用途：让角色速度逐步衰减为 0 或直接驻停。

<a id="object-action"></a>
### [`ObjectAction`](actions/object-action/index.md)
- 用途：对对象引用执行实例方法。

<a id="play-animation-wait"></a>
### [`PlayAnimationWait`](actions/play-animation-wait/index.md)
- 用途：播放指定动画并等待切出该状态。

<a id="script-start-coroutine"></a>
### [`ScriptStartCoroutine`](actions/script-start-coroutine/index.md)
- 用途：启动脚本协程。

<a id="stop"></a>
### [`Stop`](actions/stop/index.md)
- 用途：按速度归零策略停止运动。

<a id="subtree"></a>
### [`Subtree`](actions/subtree/index.md)
- 用途：加载并执行一个引用的行为树子树。

<a id="wait-for-animation-end"></a>
### [`WaitForAnimationEnd`](actions/wait-for-animation-end/index.md)
- 用途：等待动画状态退出某一初始哈希或指定状态名。

<a id="wait-for-destroy"></a>
### [`WaitForDestroy`](actions/wait-for-destroy/index.md)
- 用途：等待对象引用被销毁。

