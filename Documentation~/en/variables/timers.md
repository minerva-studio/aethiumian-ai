# Timers and admission nodes

Set a tree's `BehaviourTreeData.timeSettings` once, then select **Timer** for a
local Float variable. Each root tree owns one `BehaviourTreeTimer`; subtrees
share that instance. `Game` or `AI` selects the lifetime, while `Scaled` or
`Unscaled` selects the Unity time source. The default is Game + Scaled.

Game time follows Unity's clock regardless of that tree's lifecycle. AI time
advances only while the root is running, healthy, driven, enabled, and not
paused. It freezes at pause, driver disable, end, natural completion, or fault.
Reload creates a new timer and new variable instances; Restart and End/Start
reuse the tree timer and preserve deadlines. Pause gates tree-driver callbacks
only; it does not freeze asynchronous functions, coroutines, animation, or
physics.

`TimerVariable` starts at zero. Reading returns remaining seconds and never
starts, advances, or settles it. A positive write starts or replaces the
deadline; zero or a negative write clears it. NaN and infinity throw. A timer
is always local, fixed to Float, and is not a script binding.

The built-in Timer is local-scope only. Static and global variables cannot be
Timers, and Timer creation is explicit rather than an extensible runtime-source
factory; invalid definitions are rejected instead of being downgraded to an
ordinary `TreeVariable`.

The root behaviour tree owns the shared timer object, and subtrees inherit it.
Reads do not change timer state. AI elapsed time is settled at lifecycle
transitions rather than by per-variable or per-frame timer owners.

`Cooldown` and `Throttle` take a readable duration Float and an explicit
readable/writable Timer reference. They reject ordinary Float timer bindings and
may intentionally share one Timer. While it is positive, admission fails
without executing the child.

| Node | When the timer is written | Child failure or interruption |
| --- | --- | --- |
| Cooldown | After the child succeeds | Does not consume the timer |
| Throttle | Before starting the admitted child | Keeps the started timer |

`Countdown` is a separate Service for ordinary readable/writable Float values:
it subtracts elapsed root-tree time while the host branch is registered and
settles once on branch exit. It uses the tree's settings and rejects Timer
sources, preventing elapsed time from being counted twice.

`Timeout` also uses the tree's settings. Its deadline is evaluated by the
existing service-check schedule: an unscaled clock may advance while
`timeScale` is zero, but it does not make service checks execute during a
stopped driver or AI Pause.

## Historical names

`BranchCountdown` managed references use Unity's direct `MovedFrom` mapping to
`Countdown`; the script meta identity is retained.

For assets containing the older `Timer` Service, explicitly call
`TimerNameMigration.Migrate(tree)` in the Editor. The repair transaction
rebuilds only recognized missing timer types, preserves node/variable UUIDs,
canonicalizes names, and validates before saving. It does not guarantee recovery
of legacy per-service timing settings.
