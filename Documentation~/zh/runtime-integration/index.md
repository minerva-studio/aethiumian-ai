# 运行时集成

本页介绍如何在自己的游戏代码中驱动行为树：配置 `AI` 组件、控制执行，以及向行为树传值。各运行时类型是什么，见[核心概念](../concepts/index.md)。

## 配置 AI 组件

[Code](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/AI.cs)

在要运行行为树的 GameObject 上添加 `AI` 组件，然后设置：

| 字段 | 作用 |
| :--- | :--- |
| `data` | 要运行的 `BehaviourTreeData` 资产。没有设置时，组件会在 `Awake` 中禁用自己。 |
| `controlTarget` | 组件调用类节点操作的脚本。在编辑器中，如果行为树资产设置了 `targetScript`，`OnValidate()` 会从同一个 GameObject 上自动填入。 |
| `awakeStart` | 对象进入场景时自动启动行为树。 |
| `autoRestart` | 本次运行结束后自动开始下一次运行。 |

在代码中可以通过 `AI.Data`、`AI.ControlTarget` 以及公开字段 `awakeStart` / `autoRestart` 访问这些值。

## 生命周期

1. 组件在 `Start()` 中根据资产创建运行时 `BehaviourTree`。
2. 行为树异步初始化。在 Unity 2023.1 及以上版本，大部分工作在后台线程完成，节点初始化前会切回主线程；WebGL 上同步执行。初始化完成前发出的启动请求会被保留，等行为树就绪后执行。
3. 行为树运行中且组件未暂停时，`Update`、`LateUpdate`、`FixedUpdate` 会转发给行为树。
4. 一次运行结束且自动重启生效时，下一次 `FixedUpdate` 会开始新的运行。
5. 组件被销毁时，会结束正在运行的行为树。

用 `AI.IsRunning` 判断是否正在运行，用 `AI.BehaviourTree.IsInitialized` 判断初始化是否完成。

## 在代码中控制执行

| 方法 | 效果 |
| :--- | :--- |
| `StartBehaviourTree()` | 开始运行，之后是否自动重启取决于 `autoRestart` 字段。 |
| `Start(bool autoRestart)` | 开始运行，并设置之后是否自动重启。 |
| `Reload()` | 结束当前运行，并根据资产重建行为树。`autoRestart` 开启时会重新开始。 |
| `Reload(BehaviourTreeData data)` / `Reload(data, bool autoRestart)` | 换成另一个行为树资产并重建。 |
| `Pause()` / `Resume()` | 停止或恢复生命周期转发，执行状态会保留。 |
| `End()` | 结束当前运行。如果自动重启生效，下一次 `FixedUpdate` 会开始新的运行。 |
| `End(bool autoRestart)` | 结束当前运行并设置自动重启。要彻底停止请用 `End(false)`，它还会取消尚未执行的自动启动。 |

`Pause()` 只是让组件不再把 Unity 回调转发给行为树，不会冻结物理、协程或动画。运行模式下，组件的右键菜单里也有 `Start Behaviour Tree`、`Reload Behaviour Tree`、`Pause`、`Resume`、`End`。

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

## 向行为树传值 {#pass-values-into-the-tree}

变量按名称查找。行为树初始化完成后变量才存在；在此之前查找不到。

- `SetVariable(name, value)` 和 `SetVariable<T>(name, value)` 设置行为树变量。名称不存在时，泛型版本会输出警告。
- `SetBool`、`SetInt`、`SetFloat`、`SetVector2`、`SetVector3`、`SetVector4`、`SetColor`、`SetObject` 是按类型的快捷方法。
- `SetGlobalVariable(name, value)` 和 `SetGlobal...` 系列方法设置所有行为树共享的全局变量。全局变量在 `Project Settings > Aethiumian AI > AI Settings` 中声明，保存在 `Assets/Resources/AI/AISettings.asset`。

支持的类型见[变量系统](../variables/index.md)。

### 通过动画事件

添加一个调用 `AI` 组件上 `AnimationEvent_SetVariable` 的动画事件。事件的字符串参数填变量名；在变量名前加 `#` 表示全局变量。值取自与变量类型对应的事件参数：

| 变量类型 | 值的来源 |
| :------- | :------- |
| `Int` | int 参数 |
| `Float` | float 参数 |
| `Bool` | int 参数（非零为 `true`） |
| `UnityObject` | object 参数 |
| `Vector2` / `Vector3` / `Vector4` | 字符串参数中 `=` 后面的文本，例如 `aim=(1, 0)` |

## 错误

`BehaviourTree.IsFaulted` 表示初始化失败或运行时出错。原因保存在 `InitializationException` 或 `RuntimeFault` 中，原始异常可在 Console 中查看。出错的行为树在重新加载前不会再运行。见[故障排查](../debugging/index.md)。
