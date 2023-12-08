using Amlos.AI.Nodes;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using static Amlos.AI.BehaviourTree.NodeCallStack;

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
                Receiving,
                /// <summary>
                /// stack is waiting for next update
                /// </summary>
                WaitUntilNextUpdate,
                /// <summary>
                /// stack is waiting for an action
                /// </summary>
                Waiting,
                /// <summary>
                /// stack is NOT executing
                /// </summary>
                End,
            }

            public event Action<TreeNode> OnNodePopStack;

            protected Stack<TreeNode> callStack;
            protected TreeNode head;

            public int Count => callStack.Count;
            public bool IsPaused { get; set; }
            protected bool? Result { get; set; }
            protected TaskCompletionSource<State> CurrentAction { get; set; }
            public bool IsRunning { get; protected set; }



            /// <summary> Check whether stack is in waiting state</summary>
            public bool IsInWaitingState => State == StackState.Waiting || State == StackState.WaitUntilNextUpdate;


            /// <summary> State of the stack </summary>
            public StackState State { get; set; }
            /// <summary> Ongoing executing node </summary>
            public TreeNode Current { get; protected set; }
            /// <summary> Last executing node </summary>
            public TreeNode Previous { get; protected set; }

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
                if (Previous != null) throw new InvalidOperationException($"The behaviour tree stack is not initialized properly: Last node not null ({head.name})");
                if (State != StackState.Ready) throw new InvalidOperationException($"The behaviour tree is not in Ready state when start. Execution abort. ({State}),({Previous?.name}),({Current?.name})");

                this.head = head;
                Push(head);
                RunStack();
            }

            /// <summary>
            /// force this call stack end, will call break first
            /// </summary>
            public void End()
            {
                BreakAll();
                End_Internal();
            }

            /// <summary>
            /// end this call stack
            /// </summary>
            protected void End_Internal()
            {
                if (State == StackState.End)
                {
                    return;
                }

                callStack.Clear();
                Current = null;
                Previous = null;
                State = StackState.End;
                IsRunning = false;
                //Debug.Log("Stack is ended");
            }

            private async void RunStack()
            {
                IsRunning = true;
                /// <summary>
                /// true when last execution request an yield <see cref="StackState.WaitUntilNextUpdate"/>
                /// </summary>
                bool hasYield = false;
                while (State != StackState.End && callStack.Count != 0 && Current == null)
                {
                    Current = callStack.Peek();
                    // if recurive executed
                    // will not check if is in waiting or yield
                    if (Previous != null && Previous == Current && !(IsInWaitingState || hasYield))
                    {
                        IsRunning = false;
                        Exceptions.RecuriveExecution(State, Previous?.name);
                        // no return is fine because method is garantee throwing exception
                    }

                    hasYield = false;
                    switch (State)
                    {
                        case StackState.Ready:
                        case StackState.Calling:
                            State = StackState.Calling;
                            CurrentAction = null;

                            TreeNode current = Current;
                            State result = current.Execute();

                            if (current != Current)
                            {
                                IsRunning = false;
                                Exceptions.PointerChanged();
                            }
                            HandleResult(result);
                            break;
                        case StackState.Receiving:
                            if (!Result.HasValue)
                            {
                                IsRunning = false;
                                Exceptions.NoReturnValue(Previous?.name, Current?.name);
                            }
                            State returnState = Current.ReceiveReturnFromChild(Result.Value);
                            HandleResult(returnState);
                            break;
                        case StackState.WaitUntilNextUpdate:
                            await Task.Yield();
                            State = StackState.Ready;
                            hasYield = true;
                            break;
                        case StackState.Waiting:
                            if (Current is not Nodes.Action action)
                            {
                                Debug.LogError("Waiting on non action");
                                return;
                            }

                            CurrentAction = new TaskCompletionSource<State>();
                            action.SetRunningTask(CurrentAction);
                            try
                            {
                                Task<State> task = CurrentAction.Task;
                                await task;
                                if (CurrentAction.Task.IsCompletedSuccessfully)
                                {
                                    HandleResult(CurrentAction.Task.Result);
                                }
                                else if (task.IsFaulted)
                                {
                                    Debug.LogException(task.Exception);
                                    HandleErrorState();
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                // yield to next cycle to determine action
                                await Task.Yield();
                            }
                            break;
                        case StackState.Invalid:
                            IsRunning = false;
                            Exceptions.InvalidState(Previous?.name, Current?.name);
                            return;
                        case StackState.End:
                        default:
                            return;
                    }

                    if (State == StackState.Invalid)
                    {
                        IsRunning = false;
                        Exceptions.InvalidState(Previous?.name, Current?.name);
                    }

                    MoveToNextNode();
#if UNITY_EDITOR
                    // debug section 
                    while (IsPaused)
                    {
                        await Task.Yield();
                    }
#endif
                }

                // check calling end stack
                if (callStack.Count == 0)
                {
                    End_Internal();
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
                    // case where node does not have a return value (usually indicate that it is an flow node, and next node has scheduled)
                    case Amlos.AI.Nodes.State.NONE_RETURN:
                        Result = null;
                        // Looping execution
                        if (callStack.Peek() == Current)
                        {
                            Exceptions.RecuriveExecution(State, Previous?.name);
                        }
                        //Debug.Log($"{Current.name} add {callStack.Peek().name} to stack");
                        break;
                    case Amlos.AI.Nodes.State.Success:
                        Current.Stop();
                        Pop();
                        //Debug.Log($"{Current.name} return true to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.Receiving;
                        Result = true;
                        break;
                    case Amlos.AI.Nodes.State.Failed:
                        Current.Stop();
                        Pop();
                        //Debug.Log($"{Current.name} return false to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.Receiving;
                        Result = false;
                        break;
                    case Amlos.AI.Nodes.State.WaitUntilNextUpdate:
                        Result = null;
                        State = StackState.WaitUntilNextUpdate;
                        break;
                    case Amlos.AI.Nodes.State.Wait:
                        Result = null;
                        State = StackState.Waiting;
                        break;
                    case Amlos.AI.Nodes.State.Error:
                    default:
                        HandleErrorState(result);
                        break;
                }
            }

            private void HandleErrorState(State result = Amlos.AI.Nodes.State.Error)
            {
                Result = null;
                Debug.LogException(new InvalidOperationException($"The node return invalid state. Execution Paused. ({result})({Current?.name})"));
                IsPaused = true;
            }

            /// <summary>
            /// Roll back the entire stack
            /// </summary> 
            private void BreakAll()
            {
                Previous = null;
                Current = null;
                while (callStack.Count > 0)
                {
                    RollBack();
                }
                State = StackState.Ready;
            }

            /// <summary>
            /// Roll back the stack to certain point
            /// </summary>
            /// <param name="stopAt"></param>
            public void Break(TreeNode stopAt)
            {
                // stop at is not null and it is not a valid stop point, then it is invalid
                if (stopAt != null && !callStack.Contains(stopAt))
                {
                    throw new InvalidOperationException("Given break point is not on the stack");
                }

                Previous = null;
                Current = null;
                while (callStack.Count > 0)
                {
                    if (callStack.Peek() == stopAt) break;
                    RollBack();
                }
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
                Previous = null;
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
            public void MoveToNextNode()
            {
                Previous = Current;
                Current = null;
            }

            public override string ToString()
            {
                return callStack.Count.ToString();
            }






            internal void Update()
            {
                if (Current is not Nodes.Action action)
                {
                    return;
                }
                action.Update();
            }

            internal void FixedUpdate()
            {
                if (Current is not Nodes.Action action)
                {
                    return;
                }
                action.FixedUpdate();
            }

            internal void LateUpdate()
            {
                if (Current is not Nodes.Action action)
                {
                    return;
                }
                action.LateUpdate();
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


        internal static class Exceptions
        {
            internal static void InvalidState(string prevName, string currName)
            {
                throw new InvalidOperationException($"The behaviour tree is in invalid state. Execution abort. ({StackState.Invalid}),({prevName}),({currName})");
            }

            internal static void NoReturnValue(string prevName, string currName)
            {
                throw new InvalidOperationException($"The behaviour tree cannot find return value from last node. Execution abort. ({StackState.Receiving}),({prevName}),({currName})");
            }

            internal static void PointerChanged()
            {
                throw new InvalidOperationException($"Behaviour tree current node point changed during node execution");
            }

            /// <summary>
            /// Throw Recurive exception
            /// </summary>
            /// <exception cref="InvalidOperationException"></exception>
            internal static void RecuriveExecution(StackState State, string name)
            {
                throw new InvalidOperationException($"The behaviour tree started repeating execution, execution abort. (Did you forget to call TreeNode.End() when node finish execution?) ({State}),({name})");
            }
        }
    }
}
