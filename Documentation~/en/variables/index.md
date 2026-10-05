# Variables

A tree declares named, typed variables in the AI Editor's Variables page. Nodes read and write them while the tree runs, and your code can set them through the `AI` component (see [Runtime Integration](../runtime-integration/index.md#pass-values-into-the-tree)).

## Types

Variable definitions live in [VariableType](https://github.com/minerva-studio/aethiumian-ai/blob/main/Runtime/Fields/Variables/VariableType.cs). The main variable types are:

| Type                 | VariableType  | Use                    |
| :------------------- | :------------ | :--------------------- |
| `string`             | `String`      | text                   |
| `int`                | `Int`         | integer                |
| `float`              | `Float`       | decimal number         |
| `bool`               | `Bool`        | state                  |
| `Vector2`            | `Vector2`     | 2D vector              |
| `Vector3`            | `Vector3`     | 3D vector              |
| `Vector4` / `Color`  | `Vector4`     | 4D vector or color     |
| `UnityEngine.Object` | `UnityObject` | Unity object reference |
| `object`             | `Generic`     | arbitrary object       |

`Invalid` and `Node` are hidden/internal types and are usually not selected manually in a normal variable table.

Variables with the same name are not allowed in the same tree, even if they have different types.

## Scope

Initial definitions come from the asset; each runtime `BehaviourTree` builds its own variable table when it initializes.

| Scope | Shared by |
| :---- | :-------- |
| Local (default) | Nothing; each runtime tree has its own value. |
| Static | Every runtime instance of the same tree asset. |
| Global | Every tree. Declared in `Project Settings > Aethiumian AI > AI Settings`. |

A local Float variable can also use a Timer source; see [Timers and admission nodes](timers.md) for Timer sources, Cooldown, Throttle, Countdown services, and historical name migration.

## Variables in node fields

Nodes refer to variables through these field forms:

| Declaration                | Meaning                                                           |
| :------------------------- | :---------------------------------------------------------------- |
| `float`                    | fixed constant                                                    |
| `VariableField<float>`     | float variable or constant                                        |
| `VariableReference<float>` | float variable reference                                          |
| `VariableField`            | any variable or constant; actual valid types depend on node logic |
| `VariableReference`        | any variable reference; actual valid types depend on node logic   |

Even when a non-generic field allows any variable, the node itself may only support specific types. For example, a boolean arithmetic node cannot use a `string` as a boolean argument.
