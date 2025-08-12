using Amlos.AI.Nodes;
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

            private Task stackRunningTask;

            public int Count => callStack.Count;
            public bool IsRunning => stackRunningTask?.IsCompleted != true;
            public bool IsPaused { get; set; }


            /// <summary> State of the stack </summary>
            public StackState State { get; private set; }
            /// <summary> Ongoing executing node </summary>
            public TreeNode Current { get; private set; }
            /// <summary> Last executing node </summary>
            public TreeNode Previous { get; private set; }
            /// <summary> Current stack </summary>
            public Stack<TreeNode> Nodes => callStack;


            private bool? Result { get; set; }

            public NodeCallStack()
            {
                callStack = new Stack<TreeNode>();
            }

            public void Initialize()
            {
                callStack ??= new Stack<TreeNode>();
                callStack.Clear();
                head = null;

                stackRunningTask = null;

                Current = null;
                Previous = null;
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
                stackRunningTask = RunStack();
                stackRunningTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Debug.LogException(t.Exception);
                        End();
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
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
                //Debug.Log("Stack is ended");
            }

            private async Task RunStack()
            {
                /// <summary>
                /// true when last execution request an yield <see cref="StackState.WaitUntilNextUpdate"/>
                /// </summary>
                bool waitFlag = false;
                while (State != StackState.End && callStack.Count != 0 && Current == null)
                {
                    while (IsPaused)
                    {
#if UNITY_2023_1_OR_NEWER
                        await Awaitable.NextFrameAsync();
#else
                        await Task.Yield();
#endif
                    }

                    Current = callStack.Peek();
                    // transform is missing now, destroyed already
                    if (!Current.transform)
                    {
                        End_Internal();
                        return;
                    }
                    // if recurive executed
                    // will not check if is in waiting or yield
                    bool isWaiting = State == StackState.Waiting || State == StackState.WaitUntilNextUpdate || waitFlag;
                    if (Previous != null && Previous == Current && !isWaiting)
                    {
                        throw Exceptions.RecuriveExecution(State, Previous?.name);
                        // no return is fine because method is garantee throwing exception
                    }

                    waitFlag = false;
                    switch (State)
                    {
                        case StackState.Ready:
                        case StackState.Calling:
                            State = StackState.Calling;

                            TreeNode current = Current;
                            State result;
                            // try execute node, if failed then call handle exception
                            try { result = current.Run(); }
                            catch (Exception e) { result = current.HandleException(e); }

                            // pointer not change, and result is not acceptable (non returning or yield to next frame)
                            if (current != Current && (result != Amlos.AI.Nodes.State.NONE_RETURN && result != Amlos.AI.Nodes.State.Yield))
                            {
                                throw Exceptions.PointerChanged();
                                // none return
                            }
                            HandleResult(result);
                            break;
                        case StackState.Receiving:
                            // no result received
                            if (!Result.HasValue)
                            {
                                throw Exceptions.NoReturnValue(Previous?.name, Current?.name);
                                // none return
                            }
                            State returnState = Current.ReceiveReturnFromChild(Result.Value);
                            HandleResult(returnState);
                            break;
                        case StackState.WaitUntilNextUpdate:
                            waitFlag = true;
#if UNITY_2023_1_OR_NEWER
                            await Awaitable.NextFrameAsync();
#else
                            await Task.Yield();
#endif
                            State = StackState.Ready;
                            break;
                        case StackState.Waiting:
                            // should not waiting for non actions
                            if (Current is not Nodes.Action action)
                            {
                                Debug.LogError("Waiting on non action");
                                State = StackState.Invalid;
                                return;
                            }

                            var task = action.ActionTask;
                            State r;
                            try
                            {
                                r = await task;
                            }
                            catch (OperationCanceledException)
                            {
                                // yield to next cycle to determine action 
#if UNITY_2023_1_OR_NEWER
                                await Awaitable.NextFrameAsync();
#else
                                await Task.Yield();
#endif 
                                r = Amlos.AI.Nodes.State.Failed;
                            }
                            catch (Exception)
                            {
                                // yield to next cycle to determine action 
#if UNITY_2023_1_OR_NEWER
                                await Awaitable.NextFrameAsync();
#else
                                await Task.Yield();
#endif
                                r = action.HandleException(task.Exception);
                            }

                            // pointer changed, likely due to interruption of the stack
                            // pointer unchange, likely an internal node error
                            if (Current == action)
                            {
                                HandleResult(r);
                            }
                            break;
                        case StackState.Invalid:
                            throw Exceptions.InvalidState(Previous?.name, Current?.name);
                        case StackState.End:
                        default:
                            return;
                    }

                    if (State == StackState.Invalid)
                        throw Exceptions.InvalidState(Previous?.name, Current?.name);

                    MoveToNextNode();
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
                            throw Exceptions.RecuriveExecution(State, Previous?.name);
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
                    case Amlos.AI.Nodes.State.Yield:
                        Result = null;
                        State = StackState.WaitUntilNextUpdate;
                        break;
                    case Amlos.AI.Nodes.State.WaitAction:
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
            public bool Break(TreeNode stopAt)
            {
                // stop at is not null and it is not a valid stop point, then it is invalid
                if (stopAt != null && !callStack.Contains(stopAt))
                {
                    return false;
                }

                Previous = null;
                Current = null;
                while (callStack.Count > 0)
                {
                    if (callStack.Peek() == stopAt) break;
                    RollBack();
                }
                State = StackState.Ready;
                return true;
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

                try { treeNode.Stop(); }
                catch (Exception e) { Debug.LogException(e); }

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
            internal static Exception InvalidState(string prevName, string currName)
            {
                return new InvalidOperationException($"The behaviour tree is in invalid state. Execution abort. ({StackState.Invalid}),({prevName}),({currName})");
            }

            internal static Exception NoReturnValue(string prevName, string currName)
            {
                return new InvalidOperationException($"The behaviour tree cannot find return value from last node. Execution abort. ({StackState.Receiving}),({prevName}),({currName})");
            }

            internal static Exception PointerChanged()
            {
                return new InvalidOperationException($"Behaviour tree current node point changed during node execution");
            }

            /// <summary>
            /// Throw Recurive exception
            /// </summary>
            /// <exception cref="InvalidOperationException"></exception>
            internal static Exception RecuriveExecution(StackState State, string name)
            {
                return new InvalidOperationException($"The behaviour tree started repeating execution, execution abort. (Did you forget to call TreeNode.End() when node finish execution?) ({State}),({name})");
            }
        }
    }
}
