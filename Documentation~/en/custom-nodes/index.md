# Custom Nodes

## Create a custom node

Custom nodes are ordinary public C# types in an assembly compiled into the Unity project. The editor discovers concrete `TreeNode` subclasses through Unity's `TypeCache`; they do not need to be copied into this package. See the [runtime node folders](https://github.com/minerva-studio/aethiumian-ai/tree/main/Runtime/Nodes) for the built-in organization.

Custom nodes must be implemented by inheriting from these base types of nodes:

- Action
- Arithmetics
- Call
- Determine/ComparableDetermine
- Flow (customizing new Flow nodes is strongly NOT recommended)
- Service

Please review the special requirements for each type of node

A minimal custom action can complete immediately:

```c#
using Aethiumian.AI;
using System;
using UnityEngine;

namespace MyGame.AI
{
    [Serializable]
    [NodeTip("Log a message and complete successfully.")]
    public sealed class ReportReady : Aethiumian.AI.Nodes.Action
    {
        public override void Start()
        {
            Debug.Log("The custom node ran.");
            Success();
        }
    }
}
```

The node must be public, concrete, parameterless, and derive from `TreeNode`. `Action` exposes `Start`, `Update`, `LateUpdate`, `FixedUpdate`, and `OnDestroy`; call `Success()`, `Fail()`, or `End(bool)` when the action has a final result. Use `FunctionAction` or `NodeProgress` when the work must wait for an external asynchronous operation.
