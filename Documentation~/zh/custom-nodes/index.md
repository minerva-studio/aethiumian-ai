# 自定义节点

## 创建一个自定义节点

自定义节点是编译进 Unity 项目的程序集中的普通 public C# 类型。编辑器通过 Unity 的 `TypeCache` 发现具体的 `TreeNode` 子类，不需要把代码复制到这个包中。内置节点的目录组织可以参考[运行时节点目录](https://github.com/minerva-studio/aethiumian-ai/tree/main/Runtime/Nodes)。

自定义节点可以通过继承节点的几个基类型来实现：

- Action 动作
- Arithmetics 运算
- Call 执行
- Determine/ComparableDetermine 判断
- Flow 流程节点（不推荐自定义新流程节点）
- Service 服务

请查看每一个类型的节点的特殊要求

下面是一个会立即成功的最小自定义 Action：

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

节点必须是 public、具体的、带无参构造函数，并且继承 `TreeNode`。`Action` 提供 `Start`、`Update`、`LateUpdate`、`FixedUpdate` 和 `OnDestroy` 生命周期；节点结束时调用 `Success()`、`Fail()` 或 `End(bool)`。需要等待外部异步操作时，请使用 `FunctionAction` 或 `NodeProgress`。
