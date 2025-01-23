﻿using Amlos.AI.References;
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
        /// Services
        /// </summary>
        public List<NodeReference> services = new();
        /// <summary>
        /// Tree instance of the node
        /// </summary>
        [NonSerialized]
        public BehaviourTree behaviourTree;
        /// <summary>
        /// The service head if this node is part of service node
        /// </summary>
        private Service serviceHead;

        /// <summary>
        /// action will execute when the node is forced to stop
        /// </summary>
        public event System.Action OnInterrupted;

        /// <summary>
        /// Is node currently running?
        /// </summary>
        public bool IsRunning { get; internal set; }

        /// <summary>
        /// The original node from the behaviour tree data
        /// </summary>
        public TreeNode Prototype { get; private set; }



        /// <summary> The attached script if the behaviour tree is assigned with a script </summary>
        public MonoBehaviour Script => behaviourTree.Script;
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public AI AIComponent => behaviourTree.AIComponent;
        /// <summary> The game object this component is attached to. A component is always attached to a game object. </summary>
        public GameObject gameObject => behaviourTree.gameObject;
        /// <summary> The Transform attached to this GameObject. </summary>
        public Transform transform => behaviourTree.transform;
        public bool isServiceHead => this is Service;
        public bool isInServiceRoutine => this is Service || parent?.Node?.isInServiceRoutine == true;
        public Service ServiceHead => serviceHead ??= (this is Service s ? s : (parent?.Node?.ServiceHead));





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
        public State Run()
        {
            IsRunning = true;
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
            TreeNode cloned = NodeFactory.Clone(this, GetType());
            cloned.Prototype = this;
            return cloned;
        }





        /// <summary>
        /// get children of this node (NodeReference)
        /// </summary>
        /// <returns></returns>
        public List<NodeReference> GetChildrenReference()
        {
            List<NodeReference> list = new();
            foreach (var item in GetType().GetFields())
            {
                //is parent info
                if (item.Name == nameof(parent)) continue;

                object v = item.GetValue(this);
                if (v is NodeReference r)
                {
                    list.Add(r);
                }
                else if (v is IEnumerable<NodeReference> lr)
                {
                    foreach (var reference in lr)
                        list.Add(reference);
                }
                else if (v is Probability.EventWeight ew)
                {
                    list.Add(ew.reference);
                }
                else if (v is PseudoProbability.EventWeight pew)
                {
                    list.Add(pew.reference);
                }
                else if (v is IEnumerable<Probability.EventWeight> lew)
                {
                    foreach (var weight in lew)
                        list.Add(weight.reference);
                }
                else if (v is IEnumerable<PseudoProbability.EventWeight> lpew)
                {
                    foreach (var weight in lpew)
                        list.Add(weight.reference);
                }
            }
            return list;
        }

        /// <summary>
        /// get children of this node, not from the list
        /// </summary>
        /// <returns></returns>
        public List<NodeReference> GetDirectChildrenReference()
        {
            List<NodeReference> list = new();
            foreach (var item in GetType().GetFields())
            {
                //is parent info
                if (item.Name == nameof(parent)) continue;

                object v = item.GetValue(this);
                if (v is NodeReference r)
                {
                    list.Add(r);
                }
                else if (v is Probability.EventWeight ew)
                {
                    list.Add(ew.reference);
                }
            }
            return list;
        }

        /// <summary>
        /// get children of this node (NodeReference)
        /// </summary>
        /// <param name="includeRawReference">whether include raw reference in the child (note that raw reference is not child) </param>
        /// <returns></returns>
        public List<INodeReference> GetChildrenReference(bool includeRawReference = false)
        {
            List<INodeReference> list = new();
            foreach (var item in GetType().GetFields())
            {
                //is parent info
                if (item.Name == nameof(parent)) continue;

                object v = item.GetValue(this);

                bool result = AddReference<NodeReference>(list, v)
                            || (!includeRawReference && AddReference<RawNodeReference>(list, v))
                            || AddReference<Probability.EventWeight>(list, v);
            }
            return list;

            static bool AddReference<T>(List<INodeReference> list, object v) where T : INodeReference
            {
                if (v is T r)
                {
                    list.Add(r);
                    return true;
                }
                else if (v is IEnumerable<T> lr)
                {
                    list.AddRange(lr.OfType<INodeReference>());
                    return true;
                }
                return false;
            }
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

        /// <summary>
        /// Handle the exception catched by behaviour tree setting
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public State HandleException(Exception e)
        {
            LogException(e);

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
                else if (v is List<PseudoProbability.EventWeight> lpew)
                {
                    var refer = lpew.Find(n => n.reference == child);
                    if (refer != null)
                        return baseText + lpew.IndexOf(refer).ToString();
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
                else if (v is List<PseudoProbability.EventWeight> lpew)
                {
                    var refer = lpew.Find(n => n.reference == child);
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




        protected void LogException(Exception e)
        {
#if UNITY_EDITOR
            Debug.LogException(e, gameObject);
#endif
        }

        protected void Log(object message)
        {
#if UNITY_EDITOR
            if (behaviourTree.IsDebugging) Debug.Log(message, gameObject);
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
    }
}