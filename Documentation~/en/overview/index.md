# Overview

Aethiumian.AI is a behaviour-tree toolkit for Unity. You author a tree in a visual editor, attach it to a GameObject, and the runtime executes it alongside the Unity lifecycle.

## How the pieces fit

| Piece | Role |
| :---- | :--- |
| `BehaviourTreeData` | The tree asset: nodes, variables, and execution settings. |
| AI Editor | The editor window for authoring tree assets. |
| `AI` component | Runs a tree on a GameObject and forwards Unity updates to it. |
| `BehaviourTree` | The runtime instance the `AI` component builds from the asset. |
| Nodes | The building blocks of a tree, grouped into the categories below. |

Each GameObject gets its own runtime copy of the tree, so one asset can drive many objects. Variables belong to that copy, except static variables, which are shared by every instance of the same asset, and global variables, which are shared by all trees.

## Node categories

| Category | Use |
| :------- | :-- |
| [Flow](../reference/flow.md) | Control which children run and in what order, such as `Sequence`, `Decision`, and `Loop`. |
| [Actions](../reference/actions.md) | Long-running behaviour that finishes later, such as `Walk`, `Idle`, or `PlayAnimationWait`. |
| [Calls](../reference/calls.md) | One-step operations on Unity objects and your own scripts. |
| [Determines](../reference/determines.md) | Conditions and queries that produce a value or a true/false result. |
| [Arithmetic](../reference/arithmetic.md) | Math and logic on variables. |
| [Decorators](../reference/decorator.md) | Wrap one child and change or observe its result. |
| [Services](../reference/service.md) | Run alongside a subtree, for example to interrupt it or count down. |

You can add your own nodes; see [Custom Nodes](../custom-nodes/index.md).

## Where to go next

- New to the package: [Installation](../installation/index.md), then [Getting Started](../getting-started/index.md).
- Understanding the model: [Core Concepts](../concepts/index.md) and [Variables](../variables/index.md).
- Driving trees from code: [Runtime Integration](../runtime-integration/index.md).
- Something is not working: [Troubleshooting](../debugging/index.md).
