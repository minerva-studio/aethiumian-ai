# `Countdown`

## 用途

仅在宿主分支登记期间递减一个普通 `Float`。

## 关键输入 / 输出

- 输入：`updatingVariable` (`VariableReference<float>`)，必须绑定可读写的普通
  `Float`。
- 输出：无。

## 成功 / 失败语义

- 作为服务附着在宿主上时保持就绪并让出执行权。
- 使用所属根树的 `timeSettings` 和共享计时器。
- 宿主分支注销时只结算一次经过时间。

## 重要限制

- 宿主分支之外经过的时间不会累计。
- `AI` 时间仅在根树运行、健康、由驱动执行、启用且未暂停时推进。
- 根树统一拥有共享计时对象；读取不会改变计时状态或结算 AI 生命周期时间。
- Pause 只停止树驱动回调，不冻结外部异步工作、动画、物理或协程。

## 源码链接

- [Source code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Nodes/Services/Countdown.cs)
