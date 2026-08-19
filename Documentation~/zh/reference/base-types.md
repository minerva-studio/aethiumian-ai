# 基础类型

### 基础类型

#### TreeNode (基类)

所有节点的基类

```c#
// Initialize this node when the runtime tree instance is created.
public abstract void Initialize();

// Execute the node.
public abstract State Execute();

// Receive a return value from a child node.
public virtual State ReceiveReturnFromChild(bool @return);

// Pass a return value to the parent node.
public virtual void End(bool @return);

// Stop executing the node.
public virtual void Stop();

// Set a node as the next node to execute.
public void SetNextExecute(TreeNode child);

// Return all direct child nodes.
public List<UUID> GetAllChildrenUUIDs();
```

`SetNextExecute(child)` 是 terminal handoff。调用后应立即返回它的 `NONE_RETURN`；不要在同一轮执行里调用它之后再返回另一个 `State`。

#### NodeProgress（适用于ObjectCall节点和ObjectAction节点的参数）

用于控制一个节点的执行状态。在ObjectAction与ObjectCall中，被指定的方法如果具有该参数，则该方法可以通过控制NodeProgress来实现对树的控制。

> ComponentAction 与 ComponentCall 是旧节点；当其方法符合条件时，会直接迁移到 FunctionAction 与 FunctionCall。ObjectAction 与 ObjectCall 也保留到 Function 系列节点的编辑器升级路径。这些节点仍为兼容性保留；新行为树应优先使用 FunctionAction/FunctionCall。

##### 方法

```c#
// Pause the behaviour tree.
public void Pause();

// Resume the behaviour tree.
public void Resume();

// Complete this node in the behaviour tree.
public void Complete(bool value);

// Complete this node when the MonoBehaviour is destroyed.
public void CompleteWhenDestroyed(UnityEngine.Object obj, bool value = true);
public void CompleteWhenCanceled(CancellationToken token, bool value = true);
public void CompleteWhenCompleted(Task task, bool value = true, bool canceledValue = false);
public void CompleteWhenFalse(Func<bool> condition, bool value = true);
```

##### 举例

```c#
// Example: use ObjectCall to execute the script's Attack method.
public void Attack(NodeProgress progress){
 if(...){
  // Make ObjectCall return false.
  progress.Complete(false);
 }
 // Make ObjectCall return true.
 else progress.Complete(true);
}
```

> 不要在脚本内写两个同名的方法，否则AI无法确定是哪个方法。

```c#
public void MethodName(NodeProgress node);
public bool MethodName(); // ObjectCall only.
public void MethodName();
// These overloads are invalid when they appear together.
```
