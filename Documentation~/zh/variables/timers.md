# 计时与准入节点

## 两个时间维度

在 `BehaviourTreeData.timeSettings` 中为树设置一次时间选项，再将变量源选为
**Timer**。每棵根树拥有一个 `BehaviourTreeTimer`，子树共享该实例。`Game` 或
`AI` 选择生命周期，`Scaled` 或 `Unscaled` 选择 Unity 时间来源；零值默认是
`Game + Scaled`：

| 选项 | 语义 |
| --- | --- |
| `Game` | 随 Unity 游戏时钟推进，不受该树自身 Pause、End 或禁用影响 |
| `AI` | 仅在根树运行、健康、由驱动执行、启用且未 Pause 时推进 |
| `Scaled` | 受 `Time.timeScale` 影响 |
| `Unscaled` | 不受 `Time.timeScale` 影响 |

AI 时间在 Pause、驱动禁用、End、自然结束或故障时冻结。Reload 创建新的计时器
和变量实例；Restart 及 End/Start 复用树计时器并保留 deadline。Pause 只停止树
驱动回调，不承诺冻结异步函数、协程、动画或物理。

`TimerVariable` 初始为零。读取只返回剩余秒数，不启动、不推进也不结算计时。
写入正数启动或替换 deadline，写入零或负数清零；`NaN` 和无穷值会被拒绝。
Timer 始终是 local、固定为 `Float`，不能绑定脚本成员。

内置 Timer 只支持 local scope。static/global 变量不能是 Timer；Timer 使用显式
内置创建路径，不提供可扩展的 RuntimeVariable source 工厂。非法定义会被拒绝，
不会降级成普通 `TreeVariable`。

根 BT 持有共享计时对象，子树继承该对象。读取不会改变计时状态；AI 经过时间在
生命周期转换时结算，不由每个变量或每帧单独持有计时器。

## 节点

- `Cooldown` 在 child 成功后启动绑定 Timer。
- `Throttle` 在 child 获准执行前启动绑定 Timer；child 失败或中断仍保留计时。
- 两者只接受可读的 duration Float 和可读写的 Timer 引用；多个节点可以有意共享
  一个 Timer，Timer 为正数时不执行 child。
- `Countdown` 只接受可读写的普通 Float，在宿主分支登记期间按树时间结算经过时间，
  离开时再结算一次；不接受 Timer，以免重复计算。
- `Timeout` 使用树的相同时间设置，超时只在既有 Service 检查时处理。Unscaled
  可以让时间在 `timeScale = 0` 时推进，但不会让停止的驱动或 AI Pause 中的 Service
  自动执行检查。

## 历史名称

旧的 `BranchCountdown` managed reference 通过 `MovedFrom` 映射到 `Countdown`。
旧服务名称修复不会保证恢复旧的逐服务计时设置。
