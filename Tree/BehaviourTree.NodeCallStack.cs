using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Codice.CM.Common.CmCallContext;

namespace Amlos.AI
{
    public partial class BehaviourTree
    {
        /// <summary>
        /// The call stack of inside the behaviour tree
        /// </summary>
        [Serializable]
        public class NodeCallStack
        {
            public enum StackState
            {
                Invalid = -1,
                /// <summary>
                /// stack is ready to continue after a wait, and will continue executing in this frame or next frame of the game
                /// </summary>
                Ready,
                /// <summary>
                /// stack is calling nodes
                /// </summary>
                Calling,
                /// <summary>
                /// stack is receiving return value true
                /// </summary>
                ReceivingTrue,
                /// <summary>
                /// stack is receiving return value false
                /// </summary>
                ReceivingFalse,
                /// <summary>
                /// stack is waiting for next update
                /// </summary>
                WaitUntilNextUpdate,
                /// <summary>
                /// stack is waiting for some time
                /// </summary>
                Waiting,
                /// <summary>
                /// stack is NOT executing
                /// </summary>
                End,
            }

            public event Action<TreeNode> OnNodePopStack;
            public event System.Action OnStackEnd;
            protected Stack<TreeNode> callStack;

            public int Count => callStack.Count;
            public bool IsPaused { get; set; }
            public bool PauseAfterSingleExecution { get; set; }
            public bool Result { get; protected set; }

            /// <summary> Check whether stack is in receiving state</summary>
            public bool IsInReceivingState => State == StackState.ReceivingFalse || State == StackState.ReceivingTrue;
            /// <summary> Check whether stack is in waiting state</summary>
            public bool IsInWaitingState => State == StackState.Waiting || State == StackState.WaitUntilNextUpdate;
            /// <summary> Check whether stack is in error state</summary>
            public bool IsInInvalidState => State == StackState.Invalid;

            /// <summary> State of the stack </summary>
            public StackState State { get; set; }
            /// <summary> Ongoing executing node </summary>
            public TreeNode Current { get; protected set; }
            /// <summary> Last executing node </summary>
            public TreeNode Last { get; protected set; }

            public bool Receive => State == StackState.ReceivingTrue;

            public List<TreeNode> Nodes => callStack.ShallowCloneToList();

            public NodeCallStack()
            {
                callStack = new Stack<TreeNode>();
            }

            public virtual void Initialize()
            {
                callStack ??= new Stack<TreeNode>();
                callStack.Clear();
                Current = null;
                State = StackState.Ready;
                IsPaused = false;
            }

            /// <summary>
            /// start the stack
            /// </summary>
            public void Start(TreeNode head)
            {
                if (Current != null) throw new InvalidOperationException($"The behaviour tree stack is not initialized properly: Current node not null ({head.name})");
                if (State != StackState.Ready) throw new InvalidOperationException($"The behaviour tree is not in Ready state when start. Execution abort. ({State}),({Last?.name}),({Current?.name})");

                Push(head);
                Continue();
            }

            /// <summary>
            /// End a wait state
            /// </summary>
            public void ReceiveReturn(bool ret)
            {
                Pop();
                var prevState = State;
                State = ret ? StackState.ReceivingTrue : StackState.ReceivingFalse;

                // was waiting, set current to null, reactivate stack
                if (prevState == StackState.Waiting) MoveState();
                // was not calling, meaning a waiting stage is ended
                if (prevState != StackState.Calling) Continue();
                //UnityEngine.Debug.LogError(prevState);
            }

            /// <summary>
            /// continue executing the stack
            /// </summary>
            public void Continue()
            {
                RunStack();

                // check calling end stack
                if (callStack.Count == 0 && State != StackState.End)
                {
                    End_Internal();
                }
            }

            /// <summary>
            /// force this call stack end, will call break first
            /// </summary>
            public void End()
            {
                Break();
                End_Internal();
            }

            /// <summary>
            /// end this call stack
            /// </summary>
            protected void End_Internal()
            {
                callStack.Clear();
                Current = null;
                Last = null;
                State = StackState.End;
                OnStackEnd?.Invoke();
                //Debug.Log("Stack is ended");
            }

            private void RunStack()
            {
                while (callStack.Count != 0 && (!IsPaused || IsInReceivingState) && Current == null)
                {
                    Current = callStack.Peek();
                    if (!IsInWaitingState && Last == Current && Last != null)
                    {
                        ThrowRecuriveExecution();
                        return;
                    }

                    switch (State)
                    {
                        case StackState.Ready:
                        case StackState.Calling:
                            State = StackState.Calling;
                            HandleResult(Current.Execute());
                            break;
                        case StackState.ReceivingTrue:
                            HandleResult(Current.ReceiveReturnFromChild(true));
                            break;
                        case StackState.ReceivingFalse:
                            HandleResult(Current.ReceiveReturnFromChild(false));
                            break;
                        case StackState.WaitUntilNextUpdate:
                            break;
                        case StackState.Invalid:
                            throw new InvalidOperationException($"The behaviour tree is in invalid state. Execution abort. ({StackState.Invalid}),({Last?.name}),({Current?.name})");
                        case StackState.Waiting:
                        case StackState.End:
                        default:
                            return;
                    }

                    // break the loop
                    switch (State)
                    {
                        case StackState.Invalid:
                            throw new InvalidOperationException($"The behaviour tree is in invalid state. Execution abort. ({StackState.Invalid}),({Last?.name}),({Current?.name})");
                        // stack start waiting, stop loop
                        case StackState.Waiting:
                        // stack ended early, stopped
                        case StackState.End:
                            return;
                    }

                    MoveState();


                    // debug section
#if UNITY_EDITOR 
                    if (PauseAfterSingleExecution && IsInReceivingState)
                    {
                        IsPaused = true;
                    }
#endif 
                }
            }

            private void HandleResult(State result)
            {
                // stack ended early, stopped
                if (State == StackState.End)
                {
                    return;
                }

                switch (result)
                {
                    // case where node does not have a return value (usually indicate that it is an flow node)
                    case Amlos.AI.Nodes.State.NONE_RETURN:
                        // Looping execution
                        if (callStack.Peek() == Current)
                        {
                            ThrowRecuriveExecution();
                        }
                        //Debug.Log($"{Current.name} add {callStack.Peek().name} to stack");
                        break;
                    case Amlos.AI.Nodes.State.Success:
                        Current.Stop();
                        Pop();
                        //Debug.Log($"{Current.name} return true to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.ReceivingTrue;
                        break;
                    case Amlos.AI.Nodes.State.Failed:
                        Current.Stop();
                        Pop();
                        //Debug.Log($"{Current.name} return false to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.ReceivingFalse;
                        break;
                    case Amlos.AI.Nodes.State.WaitUntilNextUpdate:
                        State = StackState.WaitUntilNextUpdate;
                        break;
                    case Amlos.AI.Nodes.State.Wait:
                        State = StackState.Waiting;
                        break;
                    case Amlos.AI.Nodes.State.Error:
                    default:
                        throw new InvalidOperationException($"The node return invalid state. Execution abort. ({result})({Current?.name})");
                }
            }

            private void ThrowRecuriveExecution()
            {
                throw new InvalidBehaviourTreeException($"The behaviour tree started repeating execution, execution abort. (Did you forget to call TreeNode.End() when node finish execution?) ({State}),({Last.name})");
            }

            /// <summary>
            /// roll back the stack to certain point
            /// </summary>
            /// <param name="to"></param>
            public void Break(TreeNode to = null)
            {
                Last = null;
                while (callStack.Count > 0)
                {
                    TreeNode treeNode = callStack.Pop();
                    if (treeNode == to) break;
                    treeNode.Stop();
                }
                Current = null;
                State = StackState.Ready;
            }

            public TreeNode Peek()
            {
                callStack.TryPeek(out var node);
                return node;
            }

            /// <summary>
            /// push a node in the stack
            /// </summary>
            /// <param name="node"></param>
            public void Push(TreeNode node)
            {
                State = StackState.Calling;
                callStack.Push(node);
                //Debug.Log($"Node {node.name} were pushed into stack");
            }

            /// <summary>
            /// remove the first node from the stack
            /// </summary>
            /// <returns></returns>
            public TreeNode Pop()
            {
                if (callStack.TryPop(out var node))
                    OnNodePopStack?.Invoke(node);
                return node;
            }

            /// <summary>
            /// roll back the stack to last node
            /// </summary>
            /// <returns></returns>
            public TreeNode RollBack()
            {
                TreeNode treeNode = Pop();
                treeNode.Stop();
                State = StackState.Calling;
                Current = null;
                Last = null;
                return treeNode;
            }

            /// <summary>
            /// clear the stack
            /// </summary>
            public void Clear()
            {
                callStack.Clear();
            }

            /// <summary>
            /// Update last state to current state and set current state to null
            /// </summary>
            public void MoveState()
            {
                Last = Current;
                Current = null;
            }

            public override string ToString()
            {
                return callStack.Count.ToString();
            }
        }



        [Serializable]
        public class ServiceStack : NodeCallStack
        {
            public readonly Service service;

            public ServiceStack(Service service)
            {
                this.service = service;
                //currentFrame = 0;
                callStack = new Stack<TreeNode>();
            }
        }

    }
}
