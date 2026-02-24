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

        public override State Execute()
        {
            if (!hasScheduled)
            {
                foreach (var item in events)
                {
                    var node = behaviourTree.GetNode(item);
                    var callStack = new BehaviourTree.NodeCallStack();
                    behaviourTree.ServiceStacks.Add(node, callStack);
                    callStack.Start(node);
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
                            if (behaviourTree.ServiceStacks.TryGetValue(node, out var callStack))
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
                            if (behaviourTree.ServiceStacks.TryGetValue(node, out var callStack))
                            {
                                if (!callStack.IsRunning)
                                {
                                    shouldEnd = true;
                                    break;
                                }
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
            foreach (var item in events)
            {
                var node = behaviourTree.GetNode(item);
                if (behaviourTree.ServiceStacks.TryGetValue(node, out var callStack))
                {
                    behaviourTree.ServiceStacks.Remove(node);
                    if (callStack.IsRunning)
                    {
                        callStack.End();
                    }
                    if (callStack.Exception != null)
                    {
                        exceptions ??= new List<Exception>(events.Length);
                        exceptions.Add(callStack.Exception);
                    }
                }
            }
            if (exceptions != null)
                return HandleException(new AggregateException(exceptions.ToArray()));

            hasScheduled = false;
            return State.Success;
        }

        public override void Initialize()
        {
            hasScheduled = false;
        }

        protected override void OnStop()
        {
            foreach (var item in events)
            {
                var node = behaviourTree.GetNode(item);
                if (behaviourTree.ServiceStacks.TryGetValue(node, out var callStack))
                {
                    if (callStack.IsRunning)
                        callStack.End();
                    behaviourTree.ServiceStacks.Remove(node);
                }
            }
        }
    }
}
