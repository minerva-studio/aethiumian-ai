using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Amlos.AI
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
        public List<NodeReference> services;
        [NonSerialized] public BehaviourTree behaviourTree;
        private Service serviceHead;
#if UNITY_EDITOR 
#endif



        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action InterruptedStopAction;


        /// <summary>
        /// The original node from the behaviour tree data
        /// </summary>
        public TreeNode Prototype { get; private set; }


        public MonoBehaviour Script => behaviourTree.Script;
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public AI AIComponent => behaviourTree.gameObject.GetComponent<AI>();
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public GameObject gameObject => behaviourTree.gameObject;
        /// <summary> The Transform attached to this GameObject. </summary>
        public Transform transform => behaviourTree.gameObject.transform;
        public bool isServiceHead => this is Service;
        public bool isInServiceRoutine => this is Service || parent?.Node?.isInServiceRoutine == true;
        public Service ServiceHead => serviceHead ??= (this is Service s ? s : (parent?.Node?.ServiceHead));

        public TreeNode()
        {
            name = string.Empty;
            uuid = UUID.NewUUID();
        }



        /// <summary>
        /// Initialized the node, get all reference of nodes from <code>behaviourTree.References </code>
        /// <br></br>
        /// Call when behaviour tree is contructing
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// execute the node 
        /// <br></br>
        /// Call when behaviour tree runs to this node
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// deal the return from child
        /// only call from the child of the node
        /// </summary>
        /// <param name="return"></param>
        public virtual void ReceiveReturnFromChild(bool @return)
        {
            End(true);
        }

        /// <summary>
        /// return node, back to its parent
        /// </summary>
        /// <param name="return"></param>
        public virtual void End(bool @return)
        {
            //trying to end other node
            if (!isInServiceRoutine && behaviourTree.CurrentStage != this) return;

            Stop();
            behaviourTree.ReceiveReturn(this, @return);
        }

        /// <summary>
        /// set the node as the current stage to the tree
        /// </summary>
        public void SetNextExecute(TreeNode child)
        {
            //Debug.Log("Add " + name + " to progess stack");
            behaviourTree.ExecuteNext(child);
        }

        /// <summary>
        /// Force to stop the node execution
        /// <br></br>
        /// Note this is dangerous if you call the method outside the tree
        /// </summary>
        public virtual void Stop()
        {
            //execute event once only and then clear all registered event
            InterruptedStopAction?.Invoke();
            InterruptedStopAction = null;
        }

        /// <summary>
        /// clone the node
        /// </summary>
        /// <returns> a deep copy of this node, and the prototype of the new node will be this node</returns>
        public virtual TreeNode Clone()
        {
            TreeNode cloned = Clone(this, GetType());
            cloned.Prototype = this;
            return cloned;
        }

        /// <summary>
        /// get children of this node (NodeReference)
        /// </summary>
        /// <returns></returns>
        public List<NodeReference> GetChildrenReference()
        {
            List<NodeReference> list = new List<NodeReference>();
            foreach (var item in GetType().GetFields())
            {
                //is parent info
                if (item.Name == nameof(parent))
                {
                    continue;
                }
                object v = item.GetValue(this);
                if (v is NodeReference r)
                {
                    list.Add(r);
                }
                else if (v is Probability.EventWeight ew)
                {
                    list.Add(ew.reference);
                }
                else if (v is List<NodeReference> lr)
                {
                    list.AddRange(lr);
                }
                else if (v is List<Probability.EventWeight> lew)
                {
                    list.AddRange(lew.Select(e => e.reference));
                }
            }
            return list;
        }

        /// <summary>
        /// Check whether given node is child of the this node
        /// </summary>
        /// <remarks>RUNTIME ONLY</remarks>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        public bool IsParentOf(TreeNode treeNode)
        {
            foreach (var item in GetChildrenReference())
            {
                if (item == treeNode)
                {
                    return true;
                }
                if (item.Node.IsParentOf(treeNode))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get a node reference object
        /// </summary>
        /// <returns></returns>
        public NodeReference ToReference()
        {
            return new NodeReference() { UUID = uuid, Node = this };
        }

        public RawNodeReference ToRawReference()
        {
            return new RawNodeReference() { UUID = uuid, Node = this };
        }


#if UNITY_EDITOR
        /// <summary>
        /// convert the node to generic Service
        /// </summary>
        /// <returns>the uni-type generic form of the node</returns>
        //public GenericTreeNode ToGenericTreeNode()
        //{
        //    return GenericTreeNodeManager.ToGenericNode(this);
        //}

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

        public virtual string GetOrderInfo(TreeNode child)
        {
            string baseText = child.isServiceHead ? $"Service {name}:" : $"{name}: ";
            foreach (System.Reflection.FieldInfo item in GetType().GetFields())
            {
                object v = item.GetValue(this);
                if (v is NodeReference r && r == child)
                {
                    return baseText + item.Name;
                }
                else if (v is List<NodeReference> lr && lr.Contains(child))
                {
                    return baseText + lr.IndexOf(child).ToString();
                }
                else if (v is List<Probability.EventWeight> lew)
                {
                    var refer = lew.Find(n => n.reference == child);
                    if (refer != null)
                        return baseText + lew.IndexOf(refer).ToString();
                }
            }
            return string.Empty;
        }

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
                    var refer = lew.Find(n => n.reference == child);
                    if (refer != null)
                        return lew.IndexOf(refer) + 1;
                }
            }
            return 0;
        }

#endif

        public override string ToString()
        {
            return name + " (" + GetType().Name + ")\n";// + JsonUtility.ToJson(this);
        }

        /// <summary>
        /// Perform a deep copy of the object via serialization.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        protected static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", nameof(source));
            }

            return JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        }

        /// <summary>
        /// Perform a deep copy of the object via serialization.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        protected static TreeNode Clone(TreeNode source, Type type)
        {
            return (TreeNode)JsonUtility.FromJson(JsonUtility.ToJson(source), type);
        }




        protected static void LogException(Exception e)
        {
#if UNITY_EDITOR
            Debug.LogException(e);
#endif
        }

        protected static void Log(object message)
        {
#if UNITY_EDITOR
            Debug.Log(message);
#endif
        }
    }
}