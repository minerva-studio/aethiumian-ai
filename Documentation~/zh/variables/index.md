# 变量

### Variable 变量

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

同一个行为树中不允许出现同名变量，即使类型不同。变量的初始定义来自资产，运行时 `BehaviourTree` 会为执行实例构建变量表；节点可以读取、写入或引用这些运行时变量。

Variable 在节点字段中常见的几种写法：

| 声明                       | 解释                                       |
| :------------------------- | :----------------------------------------- |
| `float`                    | 固定常量                                   |
| `VariableField<float>`     | float 变量或常量                           |
| `VariableReference<float>` | float 变量引用                             |
| `VariableField`            | 任意变量或常量，实际可用类型由节点逻辑决定 |
| `VariableReference`        | 任意变量引用，实际可用类型由节点逻辑决定   |

即使 Non-Generic 字段允许选择任意变量，节点自身仍可能只支持某些类型。例如布尔运算节点不能把 `string` 当作布尔参数使用。
