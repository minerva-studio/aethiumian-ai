# Base Type

### base type

#### TreeNode (base class)

base class for all nodes

````c#
//When the instance of the behavior tree is generated, initialize this node
public abstract void Initialize();

//execute the node
public abstract State Execute();

//Receive the return value of its own child node
public virtual State ReceiveReturnFromChild (bool @return);

//pass the return value to its own parent node
public virtual void End(bool @return);

//stop the execution of this node
public virtual void Stop();

//List a node as the next node to execute
public void SetNextExecute (TreeNode child);

//Return all direct child nodes of this node
public List<UUID> GetAllChildrenUUIDs();
````

`SetNextExecute(child)` is a terminal handoff. Return its `NONE_RETURN` immediately; do not call it and then return another `State` from the same execution turn.

#### NodeProgress (parameters for ObjectCall nodes and ObjectAction nodes)

Used to control the execution state of a node. In ObjectAction and ObjectCall, if the specified method has this parameter, the method can control the tree through NodeProgress.

> ComponentAction and ComponentCall are legacy nodes that are migrating to ObjectAction and ObjectCall. Legacy nodes remain for compatibility and upgrade paths; new behaviour trees should prefer ObjectAction/ObjectCall.

##### method

````c#
//Pause the execution of Behaviour Tree
public void Pause();

//Continue the execution of Behaviour Tree
public void Resume();

//End the node execution in Behaviour Tree
public void Complete(bool value);

//When the monoBehaviour is destroyed, end the execution of the node in the Behaviour Tree
public void CompleteWhenDestroyed(UnityEngine.Object obj, bool value = true);
public void CompleteWhenCanceled(CancellationToken token, bool value = true);
public void CompleteWhenCompleted(Task task, bool value = true, bool canceledValue = false);
public void CompleteWhenFalse(Func<bool> condition, bool value = true);
````

##### Example

````c#
//example: use ObjectCall to execute the Attack method in the script
public void Attack(NodeProgress progress){
    if (...){
        //Make the ObjectCall node return false
        progress.Complete(false);
    }
    //Make the ObjectCall node return true
    else progress.Complete(true);
}
````

> Don't write two methods with the same name in the script, otherwise the AI will not be able to determine which method it is.

````c#
public void MethodName (NodeProgress node);
public bool MethodName ();//ObjectCall only
public void MethodName ();
//It is illegal when the above methods appear at the same time
````
