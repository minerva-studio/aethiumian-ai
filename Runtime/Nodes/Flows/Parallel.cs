using Amlos.AI.References;
using System;
using System.Collections.Generic;

namespace Amlos.AI.Nodes
{
    public sealed class Parallel : Flow
    {
        public enum Mode
        {
            WaitAll,
            WaitAny,
        }

        public NodeReference[] events;
        public Mode mode;

        private bool hasScheduled;
        private Dictionary<TreeNode, BehaviourTree.NodeCallStack> branchStacks = new();

        public override State Execute()
        {
            if (events == null || events.Length == 0)
            {
                return State.Success;
            }

            if (!hasScheduled)
            {
                ClearBranchStacks();
                foreach (var item in events)
                {
                    var node = behaviourTree.GetNode(item);
                    if (node == null)
                    {
                        continue;
                    }
                    if (branchStacks.ContainsKey(node))
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(node.name) ? "Parallel Branch" : $"Parallel Branch: {node.name}";
                    var stack = behaviourTree.CreateStack(BehaviourTree.StackType.Branch, label);
                    branchStacks[node] = stack;
                    behaviourTree.StartStack(stack, node);
                }

                hasScheduled = true;
                return State.Yield;
            }
            switch (mode)
            {
                case Mode.WaitAll:
                    {
                        foreach (var item in events)
                        {
                            var node = behaviourTree.GetNode(item);
                            if (node != null && branchStacks.TryGetValue(node, out var callStack))
                            {
                                if (callStack.IsRunning)
                                    return State.Yield;
                            }
                        }

                        return ResolveResult();
                    }

                default:
                case Mode.WaitAny:
                    {
                        bool shouldEnd = false;
                        foreach (var item in events)
                        {
                            var node = behaviourTree.GetNode(item);
                            if (node == null || !branchStacks.TryGetValue(node, out var callStack) || !callStack.IsRunning)
                            {
                                shouldEnd = true;
                                break;
                            }
                        }
                        if (!shouldEnd)
                            return State.Yield;

                        return ResolveResult();
                    }
            }
        }

        private State ResolveResult()
        {
            List<Exception> exceptions = null;
            foreach (var callStack in branchStacks.Values)
            {
                if (callStack.Exception != null)
                {
                    exceptions ??= new List<Exception>(branchStacks.Count);
                    exceptions.Add(callStack.Exception);
                }
            }
            ClearBranchStacks();
            hasScheduled = false;
            if (exceptions != null)
                return HandleException(new AggregateException(exceptions.ToArray()));

            return State.Success;
        }

        public override void Initialize()
        {
            ClearBranchStacks();
            hasScheduled = false;
        }

        protected override void OnStop()
        {
            hasScheduled = false;
            ClearBranchStacks();
        }

        private void ClearBranchStacks()
        {
            branchStacks ??= new();
            foreach (var stack in branchStacks.Values)
            {
                behaviourTree.EndAndUnregisterStack(stack);
            }
            branchStacks.Clear();
        }
    }
}
