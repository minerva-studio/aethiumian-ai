# Flow Nodes

`Flow` is the control-flow base category for execution-order nodes.

## Available nodes

<a id="always"></a>
### [`Always`](flow/always/index.md)

- Execute a fixed child node and return a fixed boolean value.
- [Details](flow/always/index.md)

<a id="condition"></a>
### [`Condition`](flow/condition/index.md)

- Branch between two nodes by a boolean condition.
- [Details](flow/condition/index.md)

<a id="decision"></a>
### [`Decision`](flow/decision/index.md)

- Run child nodes in order until one returns `true`.
- [Details](flow/decision/index.md)

<a id="foreach"></a>
### [`ForEach`](flow/for-each/index.md)

- Execute a node for each item in an enumerable collection.
- [Details](flow/for-each/index.md)

<a id="inverter"></a>
### [`Inverter`](flow/inverter/index.md)

- Invert a child node's boolean result.
- [Details](flow/inverter/index.md)

<a id="loop"></a>
### [`Loop`](flow/loop/index.md)

- Repeat child branches using fixed count or condition-based loop types.
- [Details](flow/loop/index.md)

<a id="parallel"></a>
### [`Parallel`](flow/parallel/index.md)

- Run unique valid child branches concurrently and wait for all branches or the first completed branch.
- [Details](flow/parallel/index.md)

<a id="pause"></a>
### [`Pause`](flow/pause/index.md)

- Pause tree execution at the current point.
- [Details](flow/pause/index.md)

<a id="probability"></a>
### [`Probability`](flow/probability/index.md)

- Select one child by weighted probability and execute it.
- [Details](flow/probability/index.md)

<a id="pseudoprobability"></a>
### [`PseudoProbability`](flow/pseudo-probability/index.md)

- Select a child by weighted probability with optional anti-repetition bias.
- [Details](flow/pseudo-probability/index.md)

<a id="restart"></a>
### [`Restart`](flow/restart/index.md)

- Reload the currently running behaviour tree.
- [Details](flow/restart/index.md)

<a id="resultchanged"></a>
### [`ResultChanged`](flow/result-changed/index.md)

- Monitor a child node and succeed when child result changes between executions.
- [Details](flow/result-changed/index.md)

<a id="rollback"></a>
### [`Rollback`](flow/rollback/index.md)

- Roll back the active node stack to a referenced node.
- [Details](flow/rollback/index.md)

<a id="sequence"></a>
### [`Sequence`](flow/sequence/index.md)

- Execute children in order and stop on the first failure.
- [Details](flow/sequence/index.md)

<a id="aggregate"></a>
### [`Aggregate`](flow/aggregate/index.md)

- Execute every child in order and aggregate all results using AND or OR.
- [Details](flow/aggregate/index.md)

<a id="wait"></a>
### [`Wait`](flow/wait/index.md)

- Wait for configured time or frame count before continuing.
- [Details](flow/wait/index.md)

<a id="yield"></a>
### [`Yield`](flow/yield/index.md)

- Yield execution for one frame.
- [Details](flow/yield/index.md)


