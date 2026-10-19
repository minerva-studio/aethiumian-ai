using Aethiumian.AI.Nodes;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Profiling;
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using static Aethiumian.AI.BehaviourTree.NodeCallStack;

namespace Aethiumian.AI
{
    public partial class BehaviourTree
    {
        /// <summary>
        /// The call stack of inside the behaviour tree
        /// </summary>
        [Serializable]
        public class NodeCallStack
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            private static readonly ProfilerMarker StackTickMarker = new("Aethiumian.AI/BehaviourTree.Stack.Tick");
            private static readonly ProfilerMarker StackUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.Update");
            private static readonly ProfilerMarker StackFixedUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.FixedUpdate");
            private static readonly ProfilerMarker StackLateUpdateMarker = new("Aethiumian.AI/BehaviourTree.Stack.LateUpdate");
#endif

            public enum EventType
            {
                Start,
                Push,
                Pop,
                Redirect,
                End,
                Exception,
            }

            /// <summary>Why a node physically left the stack; only returns carry a result.</summary>
            public enum PopReason { Return, Unwind, ForcedReturn, Clear }

            /// <summary>Diagnostic importance: scheduling is Debug; lifecycle and accepted redirects are Info. Boolean results do not set severity.</summary>
            internal enum EventSeverity { Debug, Info, Warning, Error }

            /// <summary>Structured diagnostic context. Cause IDs refer to the owning tree's retained event history.</summary>
            internal readonly struct EventContext
            {
                public readonly string Reason;
                public readonly TreeNode Source;
                public readonly long CauseEventId;
                public readonly bool? Accepted;
                public readonly Nodes.State? RequestedResult;
                public readonly string Phase;
                public readonly string Policy;
                public readonly Exception Exception;
                public readonly EventSeverity Severity;

                public EventContext(string reason = null, TreeNode source = null, long causeEventId = 0, bool? accepted = null, Nodes.State? requestedResult = null, string phase = null, string policy = null, Exception exception = null, EventSeverity severity = EventSeverity.Debug)
                {
                    Reason = reason;
                    Source = source;
                    CauseEventId = causeEventId;
                    Accepted = accepted;
                    RequestedResult = requestedResult;
                    Phase = phase;
                    Policy = policy;
                    Exception = exception;
                    Severity = severity;
                }
            }

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
#if UNITY_EDITOR
            internal event Func<NodeCallStack, EventType, TreeNode, State?, string, EventContext, long> OnStackEvent;
#endif
            internal event Action<NodeCallStack> Ended;

            protected Stack<TreeNode> callStack;
            protected TreeNode head;

            private const int maxStepsPerTick = 256;
            private bool stopAfterCurrentNode;

            public int Count => callStack.Count;
            public bool IsRunning => head != null && State != StackState.End;


            /// <summary> State of the stack </summary>
            public StackState State { get; private set; }
            /// <summary> Ongoing executing node </summary>
            public TreeNode Current { get; private set; }
            /// <summary> Last executing node </summary>
            public TreeNode Previous { get; private set; }
            /// <summary> Stack exception </summary>
            public Exception Exception { get; private set; }
            /// <summary>Gets the terminal value returned by the stack head after normal completion.</summary>
            public bool? ReturnValue => Result;
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
                Clear();
                head = null;

                Current = null;
                Previous = null;
                Exception = null;
                Result = null;
                stopAfterCurrentNode = false;
                State = StackState.Ready;

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
                Record(EventType.Start, head, context: new EventContext(severity: EventSeverity.Info));
                Push(head);
            }

            /// <summary>
            /// force this call stack end, will call break first
            /// </summary>
            public void End()
            {
                if (State == StackState.End)
                {
                    return;
                }

                var endingNode = Current ?? Peek();
                // Mark the stack terminal before callbacks run. OnInterrupted and action
                // cancellation callbacks may synchronously call End on the owning tree.
                State = StackState.End;
                BreakAll();
                CompleteEnd(endingNode, "Forced");
            }

            /// <summary>
            /// end this call stack
            /// </summary>
            protected void End_Internal(string reason = "Complete")
            {
                if (State == StackState.End)
                {
                    return;
                }

                CompleteEnd(Current ?? Previous ?? head, reason);
            }

            /// <summary>
            /// Completes a terminal transition after all forced-stop callbacks have run.
            /// The terminal state is intentionally assigned by both callers: forced end
            /// assigns it before BreakAll to make synchronous reentry harmless, while
            /// natural completion reaches this method with an already non-terminal state.
            /// </summary>
            private void CompleteEnd(TreeNode endingNode, string reason)
            {
                Clear();
                Current = null;
                Previous = null;
                State = StackState.End;
                Record(EventType.End, endingNode, context: new EventContext(reason, severity: EventSeverity.Info));
                Ended?.Invoke(this);
                //Debug.Log("Stack is ended");
            }

            /// <summary>
            /// Advances this stack during one behaviour-tree update.
            /// </summary>
            internal void Tick(bool singleNodeStep = false)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using var marker = StackTickMarker.Auto();
#endif
                stopAfterCurrentNode = false;
                bool waitFlag = State == StackState.WaitUntilNextUpdate;
                if (waitFlag)
                {
                    State = StackState.Ready;
                }

                int stepCounter = 0;
                int stepLimit = singleNodeStep ? 1 : maxStepsPerTick;
                while (State != StackState.End && callStack.Count != 0)
                {
                    if (++stepCounter > stepLimit)
                    {
                        return;
                    }

                    if (Current == null && !callStack.TryPeek(out var nextNode))
                    {
                        End_Internal();
                        return;
                    }

                    Current ??= callStack.Peek();
                    // transform is missing now, destroyed already
                    if (!Current.transform)
                    {
                        End_Internal("OwnerDestroyed");
                        return;
                    }
                    // if recurive executed
                    // will not check if is in waiting or yield
                    bool isWaiting = State == StackState.Waiting || waitFlag;
                    waitFlag = false;
                    if (Previous != null && Previous == Current && !isWaiting)
                    {
                        throw Exceptions.RecuriveExecution(State, Previous?.name);
                        // no return is fine because method is garantee throwing exception
                    }

                    switch (State)
                    {
                        case StackState.Ready:
                        case StackState.Calling:
                            State = StackState.Calling;

                            TreeNode current = Current;
                            State result;
                            // try execute node, if failed then call handle exception
                            try { result = current.Run(this); }
                            catch (Exception e)
                            {
                                result = current.HandleException(e);
                            }
                            // Ended, nothing need to do now
                            if (State == StackState.End)
                                return;
                            // pointer not change, and result is not acceptable (non returning or yield to next frame)
                            if (current != Current && (result != Aethiumian.AI.Nodes.State.NONE_RETURN && result != Aethiumian.AI.Nodes.State.Yield))
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
                            return;
                        case StackState.Waiting:
                            // should not waiting for non actions
                            if (Current is not Nodes.Action action)
                            {
                                Debug.LogError("Waiting on non action");
                                State = StackState.Invalid;
                                return;
                            }

                            if (!action.IsComplete)
                            {
                                return;
                            }

                            HandleResult(action.ResolveCompletion());
                            break;
                        case StackState.Invalid:
                            throw Exceptions.InvalidState(Previous?.name, Current?.name);
                        case StackState.End:
                        default:
                            return;
                    }

                    if (State == StackState.Invalid)
                        throw Exceptions.InvalidState(Previous?.name, Current?.name);

                    if (State == StackState.Waiting)
                    {
                        return;
                    }

                    if (Current != null)
                    {
                        MoveToNextNode();
                    }

                    if (stopAfterCurrentNode || State == StackState.WaitUntilNextUpdate)
                    {
                        return;
                    }

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
                    case Aethiumian.AI.Nodes.State.NONE_RETURN:
                        if (State == StackState.Receiving && Result.HasValue)
                        {
                            // inline boolean never push into stack so result will not be flushed.
                            // otherwise, if it is receiving but not the correct node receive the return, just reset.
                            if (callStack.Peek() != Current && callStack.Contains(Current))
                            {
                                // A receiving flow pushed a real child; execute that child instead of reusing the old result.
                                Result = null;
                                State = StackState.Calling;
                            }

                            break;
                        }

                        Result = null;
                        // Looping execution
                        if (callStack.Peek() == Current)
                        {
                            throw Exceptions.RecuriveExecution(State, Previous?.name);
                        }
                        //Debug.Log($"{Current.name} add {callStack.Peek().name} to stack");
                        break;
                    case Aethiumian.AI.Nodes.State.Success:
                        Result = true;
                        StopReturningNode();
                        Pop(PopReason.Return, result, null, 0);
                        //Debug.Log($"{Current.name} return true to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.Receiving;
                        break;
                    case Aethiumian.AI.Nodes.State.Failed:
                        Result = false;
                        StopReturningNode();
                        Pop(PopReason.Return, result, null, 0);
                        //Debug.Log($"{Current.name} return false to {Peek()?.name ?? "STACKBASE"}");
                        State = StackState.Receiving;
                        break;
                    case Aethiumian.AI.Nodes.State.Yield:
                        Result = null;
                        State = StackState.WaitUntilNextUpdate;
                        break;
                    case Aethiumian.AI.Nodes.State.WaitAction:
                        Result = null;
                        State = StackState.Waiting;
                        break;
                    case Aethiumian.AI.Nodes.State.Error:
                    default:
                        Result = null;
                        HandleErrorState(result);
                        break;
                }
            }

            private void HandleErrorState(State result = Aethiumian.AI.Nodes.State.Error)
            {
                Result = null;
                var exception = new InvalidOperationException($"Node [{Current.name}] return invalid state '({result})'.");
                if (Current.behaviourTree.RuntimeFault == null) RecordException(Current, exception, "Result", "Fault");
                Current?.behaviourTree?.LatchRuntimeFault(exception, "node invalid state");
                stopAfterCurrentNode = true;
                Debug.LogException(exception);
            }

            /// <summary>Preserves normal-return stop failures while recording their actual phase.</summary>
            private void StopReturningNode()
            {
                var returningNode = Current;
                try { returningNode.Stop(executingStack: this); }
                catch (Exception exception)
                {
                    RecordException(returningNode, exception, "Stop", $"Tree: {returningNode.behaviourTree.Prototype.treeErrorHandle}");
                    throw;
                }
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
            }

            /// <summary>
            /// Roll back the stack to certain point
            /// </summary>
            /// <param name="stopAt">The retained node to reenter, or null to unwind the entire stack.</param>
            /// <param name="source">The node requesting this redirect, when invoked by a node.</param>
            public bool Break(TreeNode stopAt, TreeNode source = null)
            {
                bool accepted = stopAt == null || callStack.Contains(stopAt);
                long causeId = Record(EventType.Redirect, stopAt, detail: accepted ? null : "Target is not in this stack.", context: new EventContext(stopAt == null ? "Unwind" : "Reenter", source, accepted: accepted, severity: accepted ? EventSeverity.Info : EventSeverity.Warning));
                // stop at is not null and it is not a valid stop point, then it is invalid
                if (!accepted)
                {
                    return false;
                }

                Previous = null;
                Current = null;
                while (callStack.Count > 0)
                {
                    if (callStack.Peek() == stopAt) break;
                    RollBack(source, causeId);
                }
                State = StackState.Ready;
                return true;
            }

            /// <summary>
            /// Interrupt a running node and return a fixed result to its parent.
            /// </summary>
            /// <param name="node">The node to stop and return from.</param>
            /// <param name="result">The result received by the node's parent.</param>
            /// <param name="source">The node requesting this redirect, when invoked by a node.</param>
            /// <returns>True if the node was found and interrupted; otherwise false.</returns>
            public bool Interrupt(TreeNode node, bool result, TreeNode source = null)
            {
                var state = result ? Aethiumian.AI.Nodes.State.Success : Aethiumian.AI.Nodes.State.Failed;
                bool accepted = node != null && callStack.Contains(node);
                long causeId = Record(EventType.Redirect, node, detail: accepted ? null : "Target is not in this stack.", context: new EventContext("Return", source, accepted: accepted, requestedResult: state, severity: accepted ? EventSeverity.Info : EventSeverity.Warning));
                if (!accepted)
                {
                    return false;
                }

                Previous = null;
                Current = null;
                while (callStack.Count > 0)
                {
                    if (callStack.Peek() == node) break;
                    RollBack(source, causeId);
                }

                if (!callStack.TryPeek(out var target) || target != node)
                {
                    return false;
                }

                Result = result;
                try { target.Stop(source, causeId, this); }
                catch (Exception e)
                {
                    RecordException(target, e, "Stop", "Continue cleanup", source, causeId, EventSeverity.Warning);
                    Debug.LogException(e);
                }
                Pop(PopReason.ForcedReturn, state, source, causeId);
                State = StackState.Receiving;
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
                Record(EventType.Push, node);
                //Debug.Log($"Node {node.name} were pushed into stack");
            }

            /// <summary>
            /// Return a Boolean value adapter without pushing it into the stack.
            /// </summary>
            /// <param name="booleanNode">The Boolean node to read.</param>
            public void ReturnInlineBoolean(Nodes.Boolean booleanNode)
            {
                Current = booleanNode;

                State result;
                try
                {
                    result = booleanNode.ReadValue() ? Aethiumian.AI.Nodes.State.Success : Aethiumian.AI.Nodes.State.Failed;
                }
                catch (Exception e)
                {
                    result = booleanNode.HandleException(e, "Execute", this);
                }

                Result = result == Aethiumian.AI.Nodes.State.Success;
                State = StackState.Receiving;
            }

            /// <summary>
            /// Removes the first node without assigning a return value; this is a manual discard.
            /// </summary>
            /// <returns></returns>
            public TreeNode Pop()
            {
                return Pop(PopReason.Clear, null, null, 0);
            }

            /// <summary>Records the physical removal with the result belonging to this exit only.</summary>
            private TreeNode Pop(PopReason reason, State? result, TreeNode source, long causeEventId)
            {
                if (!callStack.TryPop(out var node))
                {
                    return null;
                }

#if UNITY_EDITOR
                Record(EventType.Pop, node, result, context: new EventContext(reason.ToString(), source, causeEventId));
#endif
                OnNodePopStack?.Invoke(node);
                return node;
            }

            /// <summary>
            /// roll back the stack to last node
            /// </summary>
            /// <returns></returns>
            public TreeNode RollBack()
            {
                return RollBack(null, 0);
            }

            /// <summary>Unwinds one node while retaining the originating redirect's identity.</summary>
            private TreeNode RollBack(TreeNode source, long causeEventId)
            {
                //TreeNode treeNode = Pop();
                TreeNode treeNode = Peek();
                try { treeNode.Stop(source, causeEventId, this); }
                catch (Exception e)
                {
                    RecordException(treeNode, e, "Stop", "Continue cleanup", source, causeEventId, EventSeverity.Warning);
                    Debug.LogException(e);
                }
                Pop(PopReason.Unwind, null, source, causeEventId);

                if (State != StackState.End)
                    State = StackState.Calling;
                Current = null;
                Previous = null;
                return treeNode;
            }

            /// <summary>
            /// Discards all nodes without invoking Stop or node-pop callbacks.
            /// </summary>
            public void Clear()
            {
                while (callStack.TryPop(out var node))
                    Record(EventType.Pop, node, context: new EventContext(nameof(PopReason.Clear)));
            }

            /// <summary>Publishes a diagnostic event and returns its tree-owned identity, or zero outside the Editor.</summary>
            private long Record(EventType eventType, TreeNode node, State? result = null, string detail = null, EventContext context = default)
            {
#if UNITY_EDITOR
                return OnStackEvent?.Invoke(this, eventType, node, result, detail, context) ?? 0;
#else
                return 0;
#endif
            }

            /// <summary>Records a handled or escaping exception without changing its existing handling policy.</summary>
            [System.Diagnostics.Conditional("UNITY_EDITOR")]
            internal void RecordException(TreeNode node, Exception exception, string phase, string policy, TreeNode source = null, long causeEventId = 0, EventSeverity severity = EventSeverity.Error)
            {
                Record(EventType.Exception, node, detail: exception.ToString(), context: new EventContext(source: source, causeEventId: causeEventId, phase: phase, policy: policy, exception: exception, severity: severity));
            }

            /// <summary>
            /// Update last state to current state and set current state to null
            /// </summary>
            public void MoveToNextNode()
            {
                Previous = Current;
                Current = null;
            }

            /// <summary>
            /// Ends the current runner turn after the current node result is handled.
            /// This transient handoff is intentionally not a pause state, so a later
            /// explicit tree tick can continue while the owning AI remains paused.
            /// </summary>
            internal void StopAfterCurrentNode()
            {
                stopAfterCurrentNode = true;
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using var marker = StackUpdateMarker.Auto();
#endif
                action.Update();
            }

            internal void FixedUpdate()
            {
                if (Current is not Nodes.Action action)
                {
                    return;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using var marker = StackFixedUpdateMarker.Auto();
#endif
                action.FixedUpdate();
            }

            internal void LateUpdate()
            {
                if (Current is not Nodes.Action action)
                {
                    return;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                using var marker = StackLateUpdateMarker.Auto();
#endif
                action.LateUpdate();
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
            internal static Exception RecuriveExecution(StackState state, string name)
            {
                return new InvalidOperationException($"The behaviour tree started repeating execution, execution abort. (Did you forget to call TreeNode.End() when node finish execution?) ({state}),({name})");
            }
        }
    }
}
