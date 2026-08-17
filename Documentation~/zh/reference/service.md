# 服务节点

## 扩展基类（不计入公开目录）

- `Service`：所有服务节点基类。
- `RepeatService`：带间隔计数的可重复服务基类。

## 分类节点

<a id="branch"></a>
### [`Branch`](service/branch/index.md)
- 用途：为当前宿主创建一个并行分支服务。

<a id="break"></a>
### [`Break`](service/break/index.md)
- 用途：条件节点成功时中断当前服务流程。

<a id="interrupt"></a>
### [`Interrupt`](service/interrupt/index.md)
- 用途：按条件中断宿主节点并返回固定结果。

<a id="timeout"></a>
### [`Timeout`](service/timeout/index.md)
- 用途：按配置时间到达后中断宿主执行。

<a id="timer"></a>
### [`Timer`](service/timer/index.md)
- 用途：每帧更新目标变量为剩余时间。

<a id="update"></a>
### [`Update`](service/update/index.md)
- 用途：将子树作为周期性服务动作反复执行。

