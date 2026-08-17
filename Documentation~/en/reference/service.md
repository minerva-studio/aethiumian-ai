# Service Nodes

`Service` is the base class for deterministic runtime service hooks attached to host flow/action nodes.

## Available nodes

<a id="branch"></a>
### [`Branch`](service/branch/index.md)

- Start and run a service subtree branch from the host node's service slot.
- [Details](service/branch/index.md)

<a id="break"></a>
### [`Break`](service/break/index.md)

- Break current service flow when condition node succeeds.
- [Details](service/break/index.md)

<a id="interrupt"></a>
### [`Interrupt`](service/interrupt/index.md)

- Interrupt the host node when a condition becomes true.
- [Details](service/interrupt/index.md)

<a id="timeout"></a>
### [`Timeout`](service/timeout/index.md)

- Interrupt host execution after elapsed configured time.
- [Details](service/timeout/index.md)

<a id="timer"></a>
### [`Timer`](service/timer/index.md)

- Decrement and publish timer/variable value each service tick.
- [Details](service/timer/index.md)

<a id="update"></a>
### [`Update`](service/update/index.md)

- Repeatedly execute a subtree as a periodic service action.
- [Details](service/update/index.md)


