# 变量

行为树在 AI Editor 的 Variables 页面中声明带名称和类型的变量。树运行时，节点读写这些变量；你的代码也可以通过 `AI` 组件设置它们（见[运行时集成](../runtime-integration/index.md#pass-values-into-the-tree)）。

## 类型

变量定义位于 [VariableType](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Fields/Variables/VariableType.cs)。当前主要变量类型如下：

| 类型                 | VariableType  | 作用           |
| :------------------- | :------------ | :------------- |
| `string`             | `String`      | 文本           |
| `int`                | `Int`         | 整数           |
| `float`              | `Float`       | 小数           |
| `bool`               | `Bool`        | 状态           |
| `Vector2`            | `Vector2`     | 二维向量       |
| `Vector3`            | `Vector3`     | 三维向量       |
| `Vector4` / `Color`  | `Vector4`     | 四维向量或颜色 |
| `UnityEngine.Object` | `UnityObject` | Unity 对象引用 |
| `object`             | `Generic`     | 任意对象       |

`Invalid` 和 `Node` 是内部/隐藏类型，通常不在普通变量表中手动选择。

同一个行为树中不允许出现同名变量，即使类型不同。

## 作用域

变量的初始定义来自资产，每个运行时 `BehaviourTree` 在初始化时构建自己的变量表。

| 作用域 | 共享范围 |
| :----- | :------- |
| Local（默认） | 不共享，每个运行时实例各有一份。 |
| Static | 同一个行为树资产的所有运行时实例。 |
| Global | 所有行为树。在 `Project Settings > Aethiumian AI > AI Settings` 中声明。 |

Local 的 Float 变量还可以使用 Timer 来源。计时变量与计时节点的两个时间维度见[计时与准入节点](timers.md)：默认是
`Game + Scaled`。`AI.Pause()` 只停止树驱动，不承诺暂停异步函数、协程、动画或物理。

## 节点字段中的变量

Variable 在节点字段中常见的几种写法：

| 声明                       | 解释                                       |
| :------------------------- | :----------------------------------------- |
| `float`                    | 固定常量                                   |
| `VariableField<float>`     | float 变量或常量                           |
| `VariableReference<float>` | float 变量引用                             |
| `VariableField`            | 任意变量或常量，实际可用类型由节点逻辑决定 |
| `VariableReference`        | 任意变量引用，实际可用类型由节点逻辑决定   |

即使 Non-Generic 字段允许选择任意变量，节点自身仍可能只支持某些类型。例如布尔运算节点不能把 `string` 当作布尔参数使用。
