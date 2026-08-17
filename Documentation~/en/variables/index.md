# Variables

### Variable

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

Variables with the same name are not allowed in the same tree, even if they have different types. Initial definitions come from the asset; a runtime `BehaviourTree` builds the variable table for the executing instance. Nodes can read, write, or reference those runtime variables.

Common variable field forms:

| Declaration                | Meaning                                                           |
| :------------------------- | :---------------------------------------------------------------- |
| `float`                    | fixed constant                                                    |
| `VariableField<float>`     | float variable or constant                                        |
| `VariableReference<float>` | float variable reference                                          |
| `VariableField`            | any variable or constant; actual valid types depend on node logic |
| `VariableReference`        | any variable reference; actual valid types depend on node logic   |

Even when a non-generic field allows any variable, the node itself may only support specific types. For example, a boolean arithmetic node cannot use a `string` as a boolean argument.
