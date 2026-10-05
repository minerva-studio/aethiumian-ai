# Runtime Integration

This page covers driving a behaviour tree from your own game code: setting up the `AI` component, controlling execution, and passing values into the tree. See [Core Concepts](../concepts/index.md) for what each runtime type is.

## Set up the AI component

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

Add an `AI` component to the GameObject that should run the tree, then set:

| Field | Purpose |
| :---- | :------ |
| `data` | The `BehaviourTreeData` asset to run. Without it, the component disables itself in `Awake`. |
| `controlTarget` | The script that component-call nodes operate on. In the Editor, `OnValidate()` fills it from the same GameObject when the tree asset sets `targetScript`. |
| `awakeStart` | Start the tree automatically when the object enters the scene. |
| `autoRestart` | Start another run after the current run ends. |

The same values are available from code through `AI.Data`, `AI.ControlTarget`, and the public `awakeStart` / `autoRestart` fields.

## Lifecycle

1. In `Start()`, the component creates a runtime `BehaviourTree` from the asset.
2. The tree initializes asynchronously. On Unity 2023.1 or later, most of the work runs on a background thread and returns to the main thread before nodes are initialized; on WebGL it runs synchronously. A start request made before initialization finishes is kept and runs once the tree is ready.
3. While the tree is running and the component is not paused, `Update`, `LateUpdate`, and `FixedUpdate` are forwarded to it.
4. When a run ends and auto-restart is active, the next `FixedUpdate` starts a new run.
5. Destroying the component ends a running tree.

Use `AI.IsRunning` to check whether a run is in progress, and `AI.BehaviourTree.IsInitialized` to check whether initialization has finished.

## Control execution from code

| Method | Effect |
| :----- | :----- |
| `StartBehaviourTree()` | Start a run, using the `autoRestart` field for later runs. |
| `Start(bool autoRestart)` | Start a run and set whether later runs restart automatically. |
| `Reload()` | End the current run and rebuild the tree from its asset. Starts again when `autoRestart` is enabled. |
| `Reload(BehaviourTreeData data)` / `Reload(data, bool autoRestart)` | Switch to another tree asset and rebuild. |
| `Pause()` / `Resume()` | Stop or restore lifecycle forwarding. The execution state is kept. |
| `End()` | End the current run. If auto-restart is active, the next `FixedUpdate` starts a new run. |
| `End(bool autoRestart)` | End the current run and set auto-restart. Use `End(false)` to stop for good; it also cancels a pending automatic start. |

`Pause()` only stops the component from forwarding Unity callbacks to the tree. It does not freeze physics, coroutines, or animation. `Start Behaviour Tree`, `Reload Behaviour Tree`, `Pause`, `Resume`, and `End` are also available from the component's context menu in Play Mode.

```csharp
using Aethiumian.AI;
using UnityEngine;

public class EnemyAlert : MonoBehaviour
{
    [SerializeField] private AI ai;

    public void OnPlayerSpotted(Transform player)
    {
        ai.SetObject("target", player);
        ai.SetBool("alerted", true);
    }

    public void OnCutsceneStarted() => ai.Pause();
    public void OnCutsceneEnded() => ai.Resume();
}
```

## Pass values into the tree

Variables are looked up by name. A tree's variables exist once initialization has finished; before that, a lookup finds nothing.

- `SetVariable(name, value)` and `SetVariable<T>(name, value)` set a tree variable. The generic overload logs a warning when the name does not exist.
- `SetBool`, `SetInt`, `SetFloat`, `SetVector2`, `SetVector3`, `SetVector4`, `SetColor`, and `SetObject` are typed shortcuts.
- `SetGlobalVariable(name, value)` and the `SetGlobal...` shortcuts set a global variable shared by every tree. Global variables are declared in `Project Settings > Aethiumian AI > AI Settings`, stored in `Assets/Resources/AI/AISettings.asset`.

See [Variables](../variables/index.md) for the supported types.

### From animation events

Add an animation event that calls `AnimationEvent_SetVariable` on the `AI` component. The event's string parameter names the variable; prefix it with `#` to target a global variable. The value comes from the event parameter that matches the variable type:

| Variable type | Value source |
| :------------ | :----------- |
| `Int` | int parameter |
| `Float` | float parameter |
| `Bool` | int parameter (non-zero is `true`) |
| `UnityObject` | object parameter |
| `Vector2` / `Vector3` / `Vector4` | text after `=` in the string parameter, such as `aim=(1, 0)` |

## Errors

`BehaviourTree.IsFaulted` reports a failed initialization or a runtime fault. The cause is in `InitializationException` or `RuntimeFault`; check the Console for the original exception. A faulted tree does not run until it is reloaded. See [Troubleshooting](../debugging/index.md).
