using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Author: Wendell
/// </summary>
namespace Amlos.AI
{

    [Serializable]
    public class BehaviourTree
    {
        public delegate void UpdateDelegate();

        [Serializable]
        public class NodeCallStack
        {
            public enum StackState
            {
                invalid = -1,
                /// <summary>
                /// stack is ready to continue after a wait, and will continue executing in this frame or next frame of the game
                /// </summary>
                ready,
                /// <summary>
                /// stack is calling nodes
                /// </summary>
                calling,
                /// <summary>
                /// stack is receiving return value true
                /// </summary>
                receivingTrue,
                /// <summary>
                /// stack is receiving return value false
                /// </summary>
                receivingFalse,
                /// <summary>
                /// stack is waiting for next frame
                /// </summary>
                waitUntilNextFrame,
                /// <summary>
                /// stack is waiting for some time
                /// </summary>
                waiting,
                /// <summary>
                /// stack is NOT executing
                /// </summary>
                end,
            }

            protected Stack<TreeNode> callStack;
            protected StackState state;

            public int Count => callStack.Count;
            public bool IsPaused { get; set; }
            public StackState State { get => state; set { state = value; } }
            public TreeNode Current { get; private set; }
            public List<TreeNode> Nodes => callStack.ShallowClone();

            public NodeCallStack()
            {
                callStack = new Stack<TreeNode>();
            }

            public virtual void Initialize()
            {
                callStack ??= new Stack<TreeNode>();
                callStack.Clear();
                Current = null;
            }

            /// <summary>
            /// start the stack
            /// </summary>
            public void Start()
            {
                State = StackState.calling;
                Continue();
            }

            /// <summary>
            /// continue executing the stack
            /// </summary>
            public void Continue()
            {
                //current node has not yet complete
                if (Current != null)
                {
                    //if in return phase
                    switch (State)
                    {
                        case StackState.receivingTrue:
                            Current.ReceiveReturnFromChild(true);
                            break;
                        case StackState.receivingFalse:
                            Current.ReceiveReturnFromChild(false);
                            break;
                        //if not int return phase, do not do anything
                        default:
                            return;
                    }
                    State = StackState.ready;
                    Current = null;
                }
                TreeNode last = null;
                while (callStack.Count != 0 && !IsPaused && Current == null)
                {
                    Current = callStack.Peek();
                    if (State != StackState.waiting && State != StackState.waitUntilNextFrame)
                        if (last == Current && last != null)
                        {
                            throw new InvalidOperationException($"The behaviour tree started repeating executing. Execution abort ({state}),({last.name})");
                        }

                    switch (State)
                    {
                        case StackState.ready:
                        case StackState.calling:
                            State = StackState.calling;
                            Current.Execute();
                            break;
                        case StackState.receivingTrue:
                            Current.ReceiveReturnFromChild(true);
                            break;
                        case StackState.receivingFalse:
                            Current.ReceiveReturnFromChild(false);
                            break;
                        case StackState.waitUntilNextFrame:
                            Current = null;
                            break;
                        case StackState.invalid:
                        case StackState.waiting:
                        case StackState.end:
                        default:
                            goto LoopEnd;
                    }
                    last = Current;
                    Current = null;
                    continue;
                LoopEnd:
                    break;
                }

                if (callStack.Count == 0)
                {
                    Current = null;
                    State = StackState.end;
                }
            }

            /// <summary>
            /// roll back the stack to certain point
            /// </summary>
            /// <param name="to"></param>
            public void Break(TreeNode to = null)
            {
                while (callStack.Count > 0)
                {
                    TreeNode treeNode = callStack.Pop();
                    if (treeNode == to) break;
                    treeNode.Stop();
                }
                State = StackState.calling;
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
                callStack.Push(node);
            }

            /// <summary>
            /// remove the first node from the stack
            /// </summary>
            /// <returns></returns>
            public TreeNode Pop()
            {
                callStack.TryPop(out var node);
                if (Current == node)
                {
                    Current = null;
                }
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
                State = StackState.calling;
                return treeNode;
            }

            /// <summary>
            /// clear the stack
            /// </summary>
            public void Clear()
            {
                callStack.Clear();
            }


            public override string ToString()
            {
                return callStack.Count.ToString();
            }
        }

        [Serializable]
        public class ServiceStack : NodeCallStack
        {
            public Service service;
            public int currentFrame;

            public bool IsReady => currentFrame >= service.interval;

            public ServiceStack(Service service)
            {
                this.service = service;
                currentFrame = 0;
                callStack = new Stack<TreeNode>();
            }

            public override void Initialize()
            {
                currentFrame = 0;
                callStack ??= new Stack<TreeNode>();
                callStack.Clear();
            }


        }



        private const float defaultActionMaximumDuration = 60f;

        public event UpdateDelegate UpdateCall;
        public event UpdateDelegate LateUpdateCall;
        public event UpdateDelegate FixedUpdateCall;


        [SerializeField] private bool isRunning;
        [SerializeField] private bool debug = false;
        private readonly TreeNode head;
        private Dictionary<UUID, TreeNode> references;
        private Dictionary<UUID, Variable> variables;
        private MonoBehaviour script;
        private NodeCallStack mainStack;
        private Dictionary<Service, ServiceStack> serviceStacks;
        private float stageMaximumDuration;
        private float currentStageDuration;

        public float CurrentStageDuration { get => currentStageDuration; }
        public bool IsRunning { get => isRunning; set { isRunning = value; DebugLog(isRunning); } }
        public bool Continue => IsRunning && (mainStack?.IsPaused == false);
        public TreeNode Head { get => head; }
        public MonoBehaviour Script { get => script; }
        public Dictionary<UUID, TreeNode> References { get => references; }
        public BehaviourTreeData Prototype { get; set; }
        public NodeCallStack MainStack { get => mainStack; }
        public Dictionary<Service, ServiceStack> ServiceStacks { get => serviceStacks; }
        public TreeNode CurrentStage { get { return mainStack?.Current; } }



        public BehaviourTree(BehaviourTreeData behaviourTreeData, MonoBehaviour script = null)
        {
            Prototype = behaviourTreeData;
            references = new Dictionary<UUID, TreeNode>();
            variables = new Dictionary<UUID, Variable>();
            this.script = script;

            GenerateReferenceTable(Prototype.GetNodesCopy());

            head = References[behaviourTreeData.headNodeUUID];
            if (head is null) { throw new InvalidBehaviourTreeException("Invalid behaviour tree, no head was found"); }

            if (!Prototype.noActionMaximumDurationLimit)
            {
                stageMaximumDuration = behaviourTreeData.actionMaximumDuration;
                if (stageMaximumDuration == 0) stageMaximumDuration = defaultActionMaximumDuration;
            }
            AssembleReference();
        }

        /// <summary>
        /// generate the reference table of the behaviour tree
        /// </summary>
        /// <param name="nodes"></param>
        /// <exception cref="InvalidBehaviourTreeException">if behaviour tree data is invalid</exception>
        private void GenerateReferenceTable(IEnumerable<TreeNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (nodes is null)
                {
                    throw new InvalidBehaviourTreeException("A null node was found in the behaviour tree, check your behaviour tree data.");
                }
                TreeNode newInstance = node.Clone();
                references[newInstance.uuid] = newInstance;
            }
            foreach (var item in Prototype.variables)
            {
                if (item.isValid) variables[item.uuid] = new Variable(item);
            }
            //for node's null reference
            references[UUID.Empty] = null;
            //Debug.Log(nodes.Count());
            //Debug.Log(references.Count);
        }

        /// <summary>
        /// get the variable from the variable table
        /// </summary>
        /// <param name="uuid">the name of the variable table</param>
        /// <returns></returns>
        public Variable GetVariable(UUID uuid)
        {
            return variables[uuid];
        }

        /// <summary>
        /// start execute behaviour tree
        /// </summary>
        public void Start()
        {
            try
            {
                Start_Internal();
            }
            catch (Exception)
            {
                IsRunning = false;
                throw;
            }
        }

        private void Start_Internal()
        {
            IsRunning = true;
            mainStack = new NodeCallStack();
            serviceStacks = new Dictionary<Service, ServiceStack>();

            mainStack.Initialize();
            ExecuteNext(head);
            mainStack.Start();
        }

        /// <summary>
        /// let parent receive result
        /// </summary>
        /// <param name="node"></param>
        /// <param name="return"></param>
        public void ReceiveReturn(TreeNode node, bool @return)
        {
            NodeCallStack stack = GetStack(node);
            RemoveFromCallStack(stack, node);
            DebugLog(node.name + " Receiveing Return");
            DebugLog("Next: " + mainStack.Peek());
            stack.State = @return ? NodeCallStack.StackState.receivingTrue : NodeCallStack.StackState.receivingFalse;
            stack.Continue();
        }




        /// <summary>
        /// add node to the progress stack
        /// </summary>
        /// <param name="node"></param>
        public void ExecuteNext(TreeNode node)
        {
            if (node is null)
            {
                DebugLog(new InvalidBehaviourTreeException("Encounter null node in behaviour tree, behaviour tree paused"));

                switch (Prototype.errorHandle)
                {
                    case BehaviourTreeErrorSolution.pause:
                        Pause();
                        break;
                    case BehaviourTreeErrorSolution.restart:
                        Restart();
                        break;
                }
                return;
            }

            if (!node.isInServiceRoutine)
            {
                mainStack.Push(node);
                mainStack.State = NodeCallStack.StackState.calling;
                RegistryServices(node);
                ResetStageTimer();
            }
            else
            {
                NodeCallStack stack = GetStack(node);
                stack.Push(node);
                stack.State = NodeCallStack.StackState.calling;
            }
        }


        /// <summary>
        /// remove the node from its call stack, called every frame in the <see cref="AI"/> if tree is not enabled
        /// </summary>
        /// <param name="node"></param>
        private void RemoveFromCallStack(NodeCallStack stack, TreeNode node)
        {
            stack.Pop();
            //end the tree when the node is at the root
            if (!node.isInServiceRoutine)
            {
                RemoveServicesRegistry(node);
                if (stack.Count == 0) CleanUp();
            }
        }


        private NodeCallStack GetStack(TreeNode node)
        {
            return node.isInServiceRoutine ? serviceStacks[node.ServiceHead] : mainStack;
        }

        private void RegistryServices(TreeNode node)
        {
            foreach (var item in node.services)
            {
                serviceStacks[item] = new ServiceStack(item);
            }
        }

        private void RemoveServicesRegistry(TreeNode node)
        {
            foreach (var item in node.services)
            {
                var stack = serviceStacks[item];
                stack.Break();
                serviceStacks.Remove(item);
            }
            ResetStageTimer();
        }

        private void RunService(ServiceStack serviceStack)
        {
            Service service = serviceStack.service;
            //last service hasn't finished 
            if (serviceStack.Count != 0)
            {
                DebugLog($"Service {service.name} did not finish executing in expect time.");
                serviceStack.Break();
            }

            //execute
            serviceStack.Initialize();
            serviceStack.Push(service);
            serviceStack.Start();
            //Debug.Log("Service Complete");
        }

        /// <summary>
        /// end a service
        /// </summary>
        /// <param name="service"></param>
        public void EndService(Service service)
        {
            NodeCallStack stack = GetStack(service);
            stack.Pop();
        }






        /// <summary>
        /// set behaviour tree wait for the node execution finished
        /// </summary>
        /// <param name="node"></param>
        public void Wait()
        {
            DebugLog("Wait");
            mainStack.State = NodeCallStack.StackState.waiting;
        }

        /// <summary>
        /// set behaviour tree wait for the node execution finished
        /// </summary>
        /// <param name="node"></param>
        public void WaitForNextFrame()
        {
            DebugLog(mainStack.Current);
            mainStack.State = NodeCallStack.StackState.waitUntilNextFrame;
        }

        public void Pause()
        {
            mainStack.IsPaused = true;
        }

        /// <summary>
        /// break the exist progress until the progress is at the given node <paramref name="stopAt"/>
        /// </summary>
        /// <param name="stopAt"></param>
        public void Break(TreeNode stopAt = null)
        {
            while (mainStack.Count > 0)
            {
                TreeNode treeNode = mainStack.Peek();
                if (treeNode == stopAt) break;
                mainStack.RollBack();
                RemoveServicesRegistry(treeNode);
            }

            //if no more node, the tree is ended
            if (mainStack.Count == 0) CleanUp();
            //else continue tree execution in the next frame
            else mainStack.State = NodeCallStack.StackState.ready;
        }

        /// <summary>
        /// stop the tree
        /// </summary>
        public void End()
        {
            Break(null);
            CleanUp();
        }

        public void Resume()
        {
            if (mainStack.IsPaused)
            {
                mainStack.IsPaused = false;
                mainStack.State = NodeCallStack.StackState.ready;
            }
            mainStack.Continue();
        }

        /// <summary>
        /// restart the tree
        /// </summary>
        public void Restart()
        {
            DebugLog("Restart");
            AssembleReference();
            mainStack.Initialize();
            ExecuteNext(head);
            mainStack.Start();
        }




        /// <summary>
        /// Update of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        public void Update()
        {
            try
            {
                Update_Internal();
            }
            catch (Exception)
            {
                IsRunning = false;
                throw;
            }
        }


        private void Update_Internal()
        {
            //don't update when paused
            if (mainStack.IsPaused)
            {
                return;
            }
            UpdateCall?.Invoke();
        }



        /// <summary>
        /// LateUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        public void LateUpdate()
        {
            try
            {
                LateUpdate_Internal();
            }
            catch (Exception)
            {
                IsRunning = false;
                throw;
            }
        }


        private void LateUpdate_Internal()
        {
            //don't update when paused
            if (mainStack.IsPaused)
            {
                return;
            }
            LateUpdateCall?.Invoke();
        }




        /// <summary>
        /// FixedUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        public void FixedUpdate()
        {
            try
            {
                FixedUpdate_Internal();
            }
            catch (Exception)
            {
                IsRunning = false;
                throw;
            }
        }

        private void FixedUpdate_Internal()
        {
            //don't update when paused
            if (mainStack.IsPaused)
            {
                return;
            }
            if (mainStack.State == NodeCallStack.StackState.waitUntilNextFrame)
            {
                mainStack.State = NodeCallStack.StackState.ready;
            }
            if (mainStack.State == NodeCallStack.StackState.ready)
            {
                mainStack.Continue();
            }

            if (!Continue) return;
            FixedUpdateCall?.Invoke();
            if (!Continue) return;
            ServiceUpdate();
            if (!Continue) return;
            RunStageTimer();
        }




        private void AssembleReference()
        {
            foreach (var item in references.Values)
            {
                if (item is null) continue;
                if (!references.ContainsKey(item.parent) && item != head) continue;
                item.behaviourTree = this;
                references.TryGetValue(item.parent, out var parent);
                item.parent = parent;
                item.services = item.services?.Select(u => (NodeReference)References[u]).ToList() ?? new List<NodeReference>();
                foreach (var service in item.services)
                {
                    TreeNode node = (TreeNode)service;
                    node.parent = references[node.parent];
                }
                foreach (var field in item.GetType().GetFields())
                {
                    if (field.FieldType.IsSubclassOf(typeof(VariableFieldBase)))
                    {
                        var reference = (VariableFieldBase)field.GetValue(item);
                        VariableFieldBase clone = (VariableFieldBase)reference.Clone();
                        if (!clone.IsConstant)
                        {
                            bool hasVar = variables.TryGetValue(clone.UUID, out Variable variable);
                            if (hasVar) clone.SetRuntimeReference(variable);
                        }

                        field.SetValue(item, clone);
                    }
                    if (field.FieldType.IsSubclassOf(typeof(AssetReferenceBase)))
                    {
                        var reference = (AssetReferenceBase)field.GetValue(item);
                        AssetReferenceBase clone = (AssetReferenceBase)reference.Clone();
                        clone.SetAsset(Prototype.GetAsset(reference.uuid));
                        field.SetValue(item, clone);
                    }
                    if (field.FieldType.IsSubclassOf(typeof(NodeReference)))
                    {
                        var reference = (NodeReference)field.GetValue(item);
                        NodeReference clone = reference.Clone();
                        field.SetValue(item, clone);
                    }
                }
                item.Initialize();
            }
        }

        private void ServiceUpdate()
        {
            DebugLog("Service Update Start :" + mainStack);
            var stack = mainStack.Nodes;
            for (int i1 = 0; i1 < mainStack.Count; i1++)
            {
                var progress = stack[i1];
                DebugLog(progress.services.Count);
                for (int i2 = 0; i2 < progress.services.Count; i2++)
                {
                    Service service = progress.services[i2];

                    //service not found
                    if (!serviceStacks.TryGetValue(service, out var serviceStack))
                    {
                        DebugLog($"Service {service.name} did not load into the behaviour tree properly.");
                        continue;
                    }
                    DebugLog($"Service {service.name} Start");

                    //increase service timer
                    serviceStack.currentFrame++;
                    if (!serviceStack.IsReady) continue;

                    RunService(serviceStack);
                }
            }
        }




        /// <summary>
        /// Counter of the behaviour tree
        /// </summary>
        private void RunStageTimer()
        {
            currentStageDuration += Time.fixedDeltaTime;
            if (!Prototype.noActionMaximumDurationLimit && currentStageDuration >= stageMaximumDuration)
            {
                //abandon current progress, restart
                var lastCurrentStage = CurrentStage;
                Restart();
                DebugLog("Behaviour Tree waiting for node " + lastCurrentStage.name + " too long. The tree has restarted");
            }
        }

        /// <summary>
        /// set current stage to this node
        /// </summary>
        /// <param name="treeNode"></param>
        private void ResetStageTimer()
        {
            currentStageDuration = 0;
        }




        private void CleanUp()
        {
            //Debug.Log("End");
            mainStack.Clear();
            IsRunning = false;
        }

        private void DebugLog(object message)
        {
            if (debug) Debug.Log(message.ToString());
        }
    }
}
