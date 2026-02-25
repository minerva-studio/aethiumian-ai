using Amlos.AI.Accessors;
using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// Base class of all tree node related class
    /// </summary>
    [Serializable]
    public abstract class TreeNodeBase : IEquatable<TreeNodeBase>
    {
        public string name;
        public UUID uuid;
        public NodeReference parent;

        public bool Equals(TreeNodeBase other)
        {
            return other.uuid == uuid;
        }

        public override bool Equals(object obj)
        {
            return obj is TreeNodeBase && Equals((TreeNodeBase)obj);
        }

        public override int GetHashCode()
        {
            return uuid.GetHashCode();
        }
    }


    /// <summary>
    /// Base class of node in the <see cref="BehaviourTree"/>
    /// </summary>
    [Serializable]
    public abstract class TreeNode : TreeNodeBase
    {
        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action OnInterrupted;

        /// <summary>
        /// Services
        /// </summary>
        [AIInspectorIgnore]
        public List<NodeReference> services = new();

        /// <summary>
        /// Tree instance of the node
        /// </summary>
        [NonSerialized]
        [AIInspectorIgnore]
        public BehaviourTree behaviourTree;

        /// <summary>
        /// The callstack this node belongs to
        /// </summary>
        [NonSerialized]
        [AIInspectorIgnore]
        public BehaviourTree.NodeCallStack callStack;

        /// <summary>
        /// The service head if this node is part of service node, is a cached value of <see cref="ServiceHead"/> for performance
        /// </summary>
        [AIInspectorIgnore]
        private TreeNode serviceHead;


        /// <summary>
        /// Is node currently running?
        /// </summary>
        [AIInspectorIgnore]
        public bool IsRunning { get; internal set; }

        /// <summary>
        /// The original node from the behaviour tree data
        /// </summary>
        [AIInspectorIgnore]
        public TreeNode Prototype { get; private set; }



        /// <summary> The attached script if the behaviour tree is assigned with a script </summary>
        public MonoBehaviour Script => behaviourTree.Script;
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public AI AIComponent => behaviourTree.AIComponent;
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public GameObject gameObject => behaviourTree.gameObject;
        /// <summary> The Transform attached to this GameObject. </summary>
        public Transform transform => behaviourTree.transform;
        public bool isServiceHead => ServiceHead == this;
        public bool isInServiceRoutine => ServiceHead != null;
        /// <summary>
        /// The service head if this node belongs to a service stack.
        /// </summary>
        public TreeNode ServiceHead => serviceHead ??= ResolveServiceHead(this);
        public UUID UUID { get => uuid; set => uuid = value; }





        public TreeNode()
        {
            name = string.Empty;
        }



        /// <summary>
        /// Initialized the node, get all reference of nodes from <code>behaviourTree.References </code>
        /// <br/>
        /// Call when behaviour tree is constructing
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// execute the node 
        /// <br/>
        /// Call when behaviour tree runs to this node
        /// </summary>
        public abstract State Execute();

        /// <summary>
        /// Editor Condition Checks, will not run with the game in runtime
        /// </summary>
        public virtual bool EditorCheck(BehaviourTreeData tree) { return true; }

        /// <summary>
        /// deal the return from child
        /// only call from the child of the node
        /// </summary>
        /// <param name="return"></param>
        public virtual State ReceiveReturnFromChild(bool @return)
        {
            return State.Success;
        }

        /// <summary>
        /// Run the node
        /// </summary>
        /// <returns></returns>
        public State Run(BehaviourTree.NodeCallStack callStack)
        {
            this.callStack = callStack;
            this.IsRunning = true;
            return Execute();
        }

        /// <summary>
        /// Force to stop the node execution
        /// <br/>
        /// Note this is dangerous if you call the method outside the tree
        /// </summary>
        public void Stop()
        {
            //execute event once only and then clear all registered event
            OnInterrupted?.SaveInvoke();
            OnInterrupted = null;
            IsRunning = false;
            OnStop();
        }

        /// <summary>
        /// Callback when node is stopped (either force stopped or just stop)
        /// </summary>
        protected virtual void OnStop()
        {
        }

        /// <summary>
        /// clone the node
        /// </summary>
        /// <returns> a deep copy of this node, and the prototype of the new node will be this node</returns>
        public virtual TreeNode Clone()
        {
            TreeNode cloned = NodeFactory.Clone(this);
            cloned.Prototype = this;
            return cloned;
        }




        /// <summary>
        /// Check whether given node is child of the this node
        /// </summary>
        /// <remarks>RUNTIME ONLY</remarks>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        public bool IsParentOf(TreeNode treeNode)
        {
            foreach (var item in this.GetChildrenReference())
            {
                if (item == null)
                {
                    continue;
                }
                if (item.IsPointTo(treeNode))
                {
                    return true;
                }
                TreeNode childInstance = behaviourTree.GetNode(item);
                if (childInstance != null && childInstance.IsParentOf(treeNode))
                {
                    return true;
                }
            }
            return false;
        }




        /// <summary>
        /// Handle the exception catched by behaviour tree setting
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public State HandleException(Exception e)
        {
            if (behaviourTree.Prototype.nodeErrorHandle != NodeErrorSolution.Throw)
            {
                Debug.LogError($"Exception occurred at node [{name}]", gameObject);
                Debug.LogException(e, gameObject);
            }

            return behaviourTree.Prototype.nodeErrorHandle switch
            {
                NodeErrorSolution.False => State.Failed,
                NodeErrorSolution.Pause => State.Error,
                NodeErrorSolution.Throw => throw e,
                _ => State.Failed,
            };
        }

        /// <summary>
        /// Get state by result
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        protected static State StateOf(bool result)
        {
            return result ? State.Success : State.Failed;
        }





        public override string ToString()
        {
            return name + " (" + GetType().Name + ")\n";// + JsonUtility.ToJson(this);
        }

        public static implicit operator bool(TreeNode node) => node != null;







        protected void Log(object message)
        {
#if UNITY_EDITOR
            if (behaviourTree.Debugging) Debug.Log(message, gameObject);
#endif
        }

        protected static bool IsExceptionalValue(State state)
        {
            return state != State.Success && state != State.Failed && state != State.NONE_RETURN;
        }

        protected static bool IsReturnValue(State state)
        {
            return state == State.Success || state == State.Failed;
        }

        /// <summary>
        /// Resolve the service head that owns the current node by checking parent service references.
        /// </summary>
        /// <param name="node">The node to resolve from.</param>
        /// <returns>The service head if the node belongs to a service stack; otherwise null.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private TreeNode ResolveServiceHead(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            TreeNode current = node;
            TreeNode parentNode = behaviourTree.GetNode(current.parent);
            while (parentNode != null)
            {
                if (IsListedAsService(parentNode, current))
                {
                    return current;
                }

                current = parentNode;
                parentNode = behaviourTree.GetNode(current.parent);
            }

            return null;
        }

        /// <summary>
        /// Check whether the parent registers the child as a service head.
        /// </summary>
        /// <param name="parentNode">The parent node that may own services.</param>
        /// <param name="childNode">The child node that may be registered as a service.</param>
        /// <returns>True if the child is registered in the parent's service list; otherwise false.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        public static bool IsListedAsService(TreeNode parentNode, TreeNode childNode)
        {
            if (parentNode == null || childNode == null)
            {
                return false;
            }

            if (parentNode.services == null)
            {
                return false;
            }

            return parentNode.services.Any(reference => reference.UUID == childNode.uuid);
        }





        public bool CanUpgrade()
        {
            return Attribute.GetCustomAttribute(GetType(), typeof(ObsoleteAttribute)) != null;
        }

        public virtual TreeNode Upgrade()
        {
            throw new NotImplementedException();
        }

#if UNITY_EDITOR 
        /// <summary>
        /// add a service node under this node
        /// </summary>
        /// <param name="e"></param>
        public void AddService(Service e)
        {
            if (services is null) services = new List<NodeReference>();
            services.Add(e);

            e.parent = new NodeReference(uuid);
        }

        [Obsolete]
        public virtual string GetOrderInfo(TreeNode child)
        {
            string baseText = child.isServiceHead ? $"Service {name}:" : $"{name}: ";
            foreach (System.Reflection.FieldInfo item in GetType().GetFields())
            {
                object v = item.GetValue(this);
                if (v is NodeReference r && r.IsPointTo(child))
                {
                    return baseText + item.Name;
                }
                else if (v is List<NodeReference> lr && lr.Contains(child))
                {
                    return baseText + lr.IndexOf(child).ToString();
                }
                else if (v is List<Probability.EventWeight> lew)
                {
                    var refer = lew.Find(n => n.reference.IsPointTo(child));
                    if (refer != null)
                        return baseText + lew.IndexOf(refer).ToString();
                }
                else if (v is List<PseudoProbability.EventWeight> lpew)
                {
                    var refer = lpew.Find(n => n.reference.IsPointTo(child));
                    if (refer != null)
                        return baseText + lpew.IndexOf(refer).ToString();
                }
            }
            return string.Empty;
        }

        [Obsolete]
        public virtual int GetIndexInfo(TreeNode child)
        {
            string baseText = child.isServiceHead ? $"Service {name}:" : $"{name}: ";
            foreach (System.Reflection.FieldInfo item in GetType().GetFields())
            {
                object v = item.GetValue(this);
                if (v is not IEnumerable) continue;

                if (v is List<NodeReference> lr && lr.Contains(child))
                {
                    return lr.IndexOf(child) + 1;
                }
                else if (v is List<Probability.EventWeight> lew)
                {
                    var refer = lew.Find(n => n.reference.IsPointTo(child));
                    if (refer != null)
                        return lew.IndexOf(refer) + 1;
                }
                else if (v is List<PseudoProbability.EventWeight> lpew)
                {
                    var refer = lpew.Find(n => n.reference.IsPointTo(child));
                    if (refer != null)
                        return lpew.IndexOf(refer) + 1;
                }
            }
            return 0;
        }

        /// <summary>
        /// Register Right click entry in overview when in AI Editor
        /// </summary>
        /// <param name="menu"></param>
        public virtual void AddContent(UnityEditor.GenericMenu menu, BehaviourTreeData currentTree)
        {

        }
#endif
    }
}
