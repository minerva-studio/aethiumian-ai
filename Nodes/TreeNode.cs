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
        //public UUID parentUUID = UUID.Empty;

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
        //public List<UUID> servicesUUID;
        public NodeReference parent;
        public List<NodeReference> services;
        [NonSerialized] public BehaviourTree behaviourTree;


        public TreeNode Prototype { get; private set; }


        public MonoBehaviour Script => behaviourTree.Script;
        public GameObject gameObject => behaviourTree.Script.gameObject;
        public Transform transform => behaviourTree.Script.transform;
        /// <summary> whether the target script is working </summary>
        public bool enabled { get => Script.enabled; set => Script.enabled = value; }
        public bool isServiceHead { get => this is Service; }
        public bool isInServiceRoutine { get => this is Service || parent?.node?.isInServiceRoutine == true; }
        public Service ServiceHead { get => this is Service s ? s : (parent?.node?.ServiceHead); }

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
        /// stop node execution
        /// </summary>
        public virtual void Stop()
        {
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
        /// get all children of this node (uuid)
        /// </summary>
        /// <returns></returns>
        public List<NodeReference> GetAllChildrenReference()
        {
            List<NodeReference> list = new List<NodeReference>();
            foreach (var item in GetType().GetFields())
            {
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


        public NodeReference ToReference()
        {
            return new NodeReference() { uuid = uuid, node = this };
        }
        public RawNodeReference ToRawReference()
        {
            return new RawNodeReference() { uuid = uuid, node = this };
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
    }
}