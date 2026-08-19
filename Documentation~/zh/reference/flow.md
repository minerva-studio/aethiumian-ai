# 流程节点

## 扩展基类（不计入公开目录）

- `Flow`：所有流程控制节点基类。
- `ServiceHostNode`：可挂载 Service 的流程基类（基于运行时实现）。

## 分类节点

<a id="always"></a>
### [`Always`](flow/always/index.md)
- 用途：执行子节点并固定返回配置值。

<a id="condition"></a>
### [`Condition`](flow/condition/index.md)
- 用途：按布尔条件在两个节点间分支。

<a id="decision"></a>
### [`Decision`](flow/decision/index.md)
- 用途：按顺序尝试子节点，任一成功即停止。

<a id="for-each"></a>
### [`ForEach`](flow/for-each/index.md)
- 用途：对可枚举集合中的每个元素执行节点。

<a id="inverter"></a>
### [`Inverter`](flow/inverter/index.md)
- 用途：反转子节点布尔返回。

<a id="loop"></a>
### [`Loop`](flow/loop/index.md)
- 用途：按固定次数或条件循环重复执行子分支。

<a id="parallel"></a>
### [`Parallel`](flow/parallel/index.md)
- 用途：并行执行多子分支并等待全部或任一。

<a id="pause"></a>
### [`Pause`](flow/pause/index.md)
- 用途：在当前点暂停树执行。

<a id="probability"></a>
### [`Probability`](flow/probability/index.md)
- 用途：按权重随机选取一个子节点。

<a id="pseudo-probability"></a>
### [`PseudoProbability`](flow/pseudo-probability/index.md)
- 用途：按权重概率选取子节点，可带反重复偏置。

<a id="restart"></a>
### [`Restart`](flow/restart/index.md)
- 用途：重载当前行为树。

<a id="result-changed"></a>
### [`ResultChanged`](flow/result-changed/index.md)
- 用途：监听子节点并在结果变化时成功。

<a id="rollback"></a>
### [`Rollback`](flow/rollback/index.md)
- 用途：将当前执行回退到目标节点。

<a id="sequence"></a>
### [`Sequence`](flow/sequence/index.md)
- 用途：按顺序执行子节点，并在首个失败时停止。

<a id="aggregate"></a>
### [`Aggregate`](flow/aggregate/index.md)
- 用途：按顺序执行全部子节点，再使用 AND 或 OR 聚合所有结果。

<a id="wait"></a>
### [`Wait`](flow/wait/index.md)
- 用途：等待指定时间或帧数。

<a id="yield"></a>
### [`Yield`](flow/yield/index.md)
- 用途：让出一帧执行。

