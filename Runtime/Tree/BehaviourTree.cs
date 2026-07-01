#nullable enable
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Nodes;
using Aethiumian.AI.Randomization;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Aethiumian.AI
{
    /// <summary>
    /// The behaviour tree class that runs the behaviour tree
    /// </summary>
    /// <remarks>
    /// Author: Wendell
    /// </remarks>
    [Serializable]
    public partial class BehaviourTree
    {
        public delegate void UpdateDelegate();

        internal enum StackType
        {
            Main,
            Service,
            Branch,
        }

        internal readonly struct StackMetadata
        {
            public readonly StackType Type;
            public readonly string Label;
#if UNITY_EDITOR
            public readonly int DebugId;
#endif

            public StackMetadata(StackType type, string label, int debugId = 0)
            {
                Type = type;
                Label = string.IsNullOrWhiteSpace(label) ? type.ToString() : label;
#if UNITY_EDITOR
                DebugId = debugId;
#endif
            }
        }

        private static VariableTable? globalVariables;
        private static readonly Dictionary<BehaviourTreeData, VariableTable> staticVariablesDictionary = new();


        private const float defaultActionMaximumDuration = 60f;

        //internal event UpdateDelegate UpdateCall;
        //internal event UpdateDelegate LateUpdateCall;
        //internal event UpdateDelegate FixedUpdateCall;


        private readonly GameObject attachedGameObject;
        private readonly Transform attachedTransform;
        private readonly Dictionary<UUID, TreeNode?> references;
        private readonly Dictionary<TreeNode, NodeCallStack?> serviceStacks;
        private readonly Dictionary<NodeCallStack, StackMetadata> activeStacks;
        private readonly VariableTranslationTable? variableTranslations;
        private readonly VariableTable variables;
        private readonly VariableTable staticVariables;
        private readonly AIRandomSourceResolver randomSources;
        private readonly Task initer;
        private readonly MonoBehaviour script;
        private readonly AI ai;

        [SerializeField] private bool debug = false;
        private TreeNode head = null!;
        private float stageMaximumDuration;
        private NodeCallStack mainStack = null!;
        private float currentStageDuration;
#if UNITY_EDITOR
        private const int stackEventCapacity = 512;
        private readonly Queue<StackEventRecord> stackEvents = new();
        private int nextStackDebugId;
#endif

        /// <summary> How long is current stage? </summary>
        public float CurrentStageDuration => currentStageDuration;
        public bool IsInitialized => initer != null && initer.IsCompletedSuccessfully;
        public bool IsError => initer == null || (initer.IsFaulted || initer.IsCanceled);
        public bool IsRunning => mainStack?.IsRunning == true;
        public bool Debugging { get => debug; set { debug = value; } }
        /// <summary> Stop if main stack is set to pause  </summary>
        public bool IsPaused => IsRunning && (mainStack?.IsPaused == true);
        public TreeNode Head => head;
        public MonoBehaviour Script => script;
        public GameObject gameObject => attachedGameObject;
        public AI AIComponent => ai;
        public Transform transform => attachedTransform;
        public IReadOnlyDictionary<UUID, TreeNode?> References => references;
        internal VariableTable Variables => variables;
        internal VariableTable StaticVariables => staticVariables;
        public AIRandomSourceResolver RandomSources => randomSources;
        public IAIRandomSource Random => randomSources.TreeSource;
        public BehaviourTreeData Prototype { get; private set; }
        public NodeCallStack MainStack => mainStack;
        public IReadOnlyDictionary<TreeNode, NodeCallStack?> ServiceStacks => serviceStacks;
        internal IReadOnlyDictionary<NodeCallStack, StackMetadata> ActiveStacks => activeStacks;
        public TreeNode? ExecutingNode => mainStack?.Current;
        public TreeNode? LastExecutedNode => mainStack?.Previous;
        public ExecutingNodeInfo CurrentStage => new(mainStack?.Current, currentStageDuration, stageMaximumDuration);
#if UNITY_EDITOR
        internal IReadOnlyList<StackEventRecord> StackEvents => stackEvents?.ToArray() ?? Array.Empty<StackEventRecord>();
#endif

        /// <summary>
        /// Gets all running subtree nodes reachable from this behaviour tree.
        /// </summary>
        /// <returns>A list of subtree nodes that own runtime behaviour tree instances.</returns>
        public IReadOnlyList<Subtree> RunningSubtrees
        {
            get
            {
                var subtrees = new List<Subtree>();
                CollectRunningSubtrees(this, subtrees, new HashSet<BehaviourTree>());
                return subtrees;
            }
        }

        private bool CanContinue => IsRunning && (mainStack?.IsPaused == false);
        /// <summary>
        /// Global variables of the behaviour tree
        /// <br/>
        /// (The variable shared in all behaviour tree)
        /// </summary>
        internal static VariableTable GlobalVariables => globalVariables ??= BuildGlobalVariables();




        public BehaviourTree(BehaviourTreeData behaviourTreeData, GameObject gameObject, MonoBehaviour script)
            : this(behaviourTreeData, null, gameObject, script)
        {
        }

        public BehaviourTree(BehaviourTreeData behaviourTreeData, VariableTranslationTable? variableTranslations, GameObject gameObject, MonoBehaviour script)
        {
            this.Prototype = behaviourTreeData;
            this.script = script;
            this.attachedGameObject = gameObject;
            this.attachedTransform = gameObject.transform;
            this.ai = gameObject.GetComponent<AI>();

            if (!script) Debug.LogWarning("No control script assigned to AI", attachedGameObject);

            references = new();
            serviceStacks = new();
            activeStacks = new();
            variables = new VariableTable(true);
            staticVariables = GetStaticVariableTable();
            randomSources = new AIRandomSourceResolver(this);
            this.variableTranslations = variableTranslations ?? VariableTranslationTable.Empty;
            initer = Init(behaviourTreeData);
        }




        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitGlobalVariables()
        {
            globalVariables = BuildGlobalVariables();
        }

        private async Task Init(BehaviourTreeData behaviourTreeData)
        {
            try
            {
#if UNITY_WEBGL
            InitializationTask();
#elif UNITY_2023_1_OR_NEWER
                await Awaitable.BackgroundThreadAsync();
                InitializationTask();
                await Awaitable.MainThreadAsync();
#else
            // try run in different thread, theorectically possible, but not sure
            await Task.Run(() => InitializationTask(behaviourTreeData));
#endif
                InitializeNodes();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        private void InitializationTask()
        {
            GenerateNodeReferenceTable();
            GenerateVariableTable();

            var behaviourTreeData = Prototype;
            head = References[behaviourTreeData.headNodeUUID] ?? throw new InvalidBehaviourTreeException("Invalid behaviour tree, no head was found");
            if (!Prototype.noActionMaximumDurationLimit)
            {
                stageMaximumDuration = behaviourTreeData.actionMaximumDuration;
                if (stageMaximumDuration == 0) stageMaximumDuration = defaultActionMaximumDuration;
            }

            AssembleReference();
        }





        /// <summary>
        /// start execute behaviour tree
        /// </summary>
        public void Start()
        {
            if (IsRunning) return;

            try
            {
                Start_Internal();
            }
            catch (Exception)
            {
                mainStack.End();
                throw;
            }
        }

        private void Start_Internal()
        {
            Start_Internal(head, "Main Stack");
        }

        private void Start_Internal(TreeNode startNode, string stackLabel)
        {
            EndAllStacks();
            serviceStacks.Clear();
            mainStack = CreateStack(StackType.Main, stackLabel);

            mainStack.Initialize();
            RegistryServices(startNode);
            ResetStageTimer();
            mainStack.Start(startNode);
        }

        /// <summary>
        /// Starts the current run from the given runtime node without changing the tree's normal head.
        /// </summary>
        /// <param name="target">The runtime node to use as this run's root.</param>
        /// <returns>True when the forced run was started; otherwise false.</returns>
        public bool StartFromNode(TreeNode target)
        {
            if (!IsInitialized || target == null)
            {
                return false;
            }

            if (!references.TryGetValue(target.uuid, out TreeNode? runtimeTarget)
                || !ReferenceEquals(runtimeTarget, target))
            {
                return false;
            }

            try
            {
                Start_Internal(target, "Forced Main Stack");
                return true;
            }
            catch (Exception)
            {
                mainStack.End();
                throw;
            }
        }

        /// <summary>
        /// Starts the current run from the runtime node matching the given UUID.
        /// </summary>
        /// <param name="targetUuid">The runtime node UUID to use as this run's root.</param>
        /// <returns>True when the forced run was started; otherwise false.</returns>
        public bool StartFromNode(UUID targetUuid)
        {
            if (!IsInitialized
                || !references.TryGetValue(targetUuid, out TreeNode? target)
                || target == null)
            {
                return false;
            }

            return StartFromNode(target);
        }




        /// <summary>
        /// Add node to the progress stack
        /// </summary>
        /// <param name="nodeReference"></param>
        /// <param name="callStack"></param>
        internal bool ExecuteNext(NodeReference nodeReference, NodeCallStack callStack)
        {
            var node = GetNode(nodeReference);
            if (node is null)
            {
                HandleNullNode();
                return false;
            }

            if (node is Aethiumian.AI.Nodes.Boolean booleanNode)
            {
                callStack.ReturnInlineBoolean(booleanNode);
                return true;
            }

            callStack.Push(node);
            RegistryServices(node);

            if (callStack == mainStack)
            {
                ResetStageTimer();
            }

            return true;
        }

        private void HandleNullNode()
        {
            Debug.LogException(new InvalidOperationException("Encounter null node"));
            switch (Prototype.treeErrorHandle)
            {
                case BehaviourTreeErrorSolution.Pause:
                    Pause();
                    break;
                case BehaviourTreeErrorSolution.Restart:
                    Restart();
                    break;
                case BehaviourTreeErrorSolution.Throw:
                    Pause();
                    throw new InvalidBehaviourTreeException("Encounter null node in behaviour tree, behaviour tree Paused");
            }
        }

        /// <summary>
        /// Check node is in progress (in a stack's top)
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        internal bool IsNodeInProgress(TreeNode treeNode)
        {
            foreach (var stack in GetActiveStacksSnapshot())
            {
                if (stack?.Current == treeNode) return true;
            }
            return false;
        }








        public bool Pause()
        {
            if (!IsRunning) return false;

            mainStack.IsPaused = true;
            return true;
        }

        /// <summary>
        /// stop the tree (main stack)
        /// </summary> 
        public bool End()
        {
            if (!IsRunning) return false;

            EndAllStacks();
            return true;
        }

        public bool Resume()
        {
            if (!IsRunning) return false;
            if (mainStack.IsPaused) mainStack.IsPaused = false;
            return true;
        }

        /// <summary>
        /// restart the tree
        /// </summary>
        public void Restart()
        {
            Log("Restart");
            EndAllStacks();
            serviceStacks.Clear();
            AssembleReference();
            InitializeNodes();
            mainStack = CreateStack(StackType.Main, "Main Stack");
            mainStack.Initialize();
            RegistryServices(head);
            ResetStageTimer();
            mainStack.Start(head);
        }





        /// <summary>
        /// Update of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        internal void Update()
        {
            //don't update when paused
            if (!CanContinue) return;

            foreach (var stack in GetActiveStacksSnapshot())
            {
                Try(stack.Update);
            }

        }

        /// <summary>
        /// LateUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        internal void LateUpdate()
        {
            //don't update when paused
            if (!CanContinue) return;

            foreach (var stack in GetActiveStacksSnapshot())
            {
                Try(stack.LateUpdate);
            }

        }

        /// <summary>
        /// FixedUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        internal void FixedUpdate()
        {
            //don't update when paused  
            if (!CanContinue) return;
            foreach (var stack in GetActiveStacksSnapshot())
            {
                if (!CanContinue) return;
                Try(stack.FixedUpdate);
            }

            if (!CanContinue) return;
            Try(ServiceUpdate);

            if (!CanContinue) return;
            RunStageTimer();
        }




        #region Stack Management

        /// <summary>
        /// Create a snapshot of every stack currently registered on this tree.
        /// </summary>
        /// <returns>A list containing the main stack and registered service or branch stacks.</returns>
        internal List<NodeCallStack> GetActiveStacksSnapshot()
        {
            return activeStacks.Keys.Where(stack => stack != null).ToList();
        }

        /// <summary>
        /// Create and activate a stack with behaviour tree runtime metadata.
        /// </summary>
        /// <param name="type">The external stack category used by tree-level tools.</param>
        /// <param name="label">The debug label displayed by editor tooling.</param>
        /// <returns>The active stack.</returns>
        internal NodeCallStack CreateStack(StackType type, string label)
        {
            var stack = new NodeCallStack();
            ActivateStack(stack, type, label);
            return stack;
        }

        /// <summary>
        /// Activate a stack so the tree can tick it and scan services from it.
        /// </summary>
        /// <param name="stack">The stack to activate.</param>
        /// <param name="type">The external stack category used by tree-level tools.</param>
        /// <param name="label">The debug label displayed by editor tooling.</param>
        internal void ActivateStack(NodeCallStack stack, StackType type, string label)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));

            if (activeStacks.ContainsKey(stack))
            {
                DeactivateIdleStack(stack);
            }

#if UNITY_EDITOR
            var metadata = new StackMetadata(type, label, ++nextStackDebugId);
#else
            var metadata = new StackMetadata(type, label);
#endif
            activeStacks[stack] = metadata;
            stack.OnNodePopStack += RemoveServicesRegistry;
#if UNITY_EDITOR
            stack.OnStackEvent += RecordStackEvent;
#endif
        }

        /// <summary>
        /// Start a registered stack from a head node.
        /// </summary>
        /// <param name="stack">The stack that will execute the node.</param>
        /// <param name="head">The node to execute first.</param>
        internal void StartStack(NodeCallStack stack, TreeNode head)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (head == null) throw new ArgumentNullException(nameof(head));

            stack.Initialize();
            RegistryServices(head);
            stack.Start(head);
        }

        /// <summary>
        /// Deactivate an idle stack without ending its lifecycle.
        /// </summary>
        /// <param name="stack">The idle stack to remove from active ticking.</param>
        internal void DeactivateIdleStack(NodeCallStack stack)
        {
            if (stack == null || !activeStacks.ContainsKey(stack))
            {
                return;
            }

            if (stack.IsRunning || stack.Count > 0 || stack.State != NodeCallStack.StackState.End)
            {
                throw new InvalidOperationException("Only an ended idle stack can be deactivated.");
            }

            DetachStack(stack);
        }

        /// <summary>
        /// End a stack lifecycle and remove it from active ticking.
        /// </summary>
        /// <param name="stack">The stack to end.</param>
        internal void EndStack(NodeCallStack stack)
        {
            if (stack == null)
            {
                return;
            }

            if (stack.IsRunning || stack.Count > 0 || stack.State != NodeCallStack.StackState.End)
            {
                stack.End();
            }

            DetachStack(stack);
        }

        private void DetachStack(NodeCallStack stack)
        {
            if (stack == null || !activeStacks.ContainsKey(stack))
            {
                return;
            }

            stack.OnNodePopStack -= RemoveServicesRegistry;
#if UNITY_EDITOR
            stack.OnStackEvent -= RecordStackEvent;
#endif
            activeStacks.Remove(stack);
        }

        #endregion

        private void EndAllStacks()
        {
            EndAllServiceStacks();

            foreach (var stack in activeStacks.Keys.ToArray())
            {
                EndStack(stack);
            }
            activeStacks.Clear();
        }

        private void EndAllServiceStacks()
        {
            foreach (var pair in serviceStacks.ToArray())
            {
                if (!serviceStacks.ContainsKey(pair.Key))
                {
                    continue;
                }

                var stack = pair.Value;
                if (stack != null)
                {
                    EndStack(stack);
                }
                if (pair.Key is Service service)
                {
                    service.OnUnregistered();
                }
            }
            serviceStacks.Clear();
        }




        /// <summary>
        /// Counter of the behaviour tree
        /// </summary>
        private void RunStageTimer()
        {
            currentStageDuration += Time.fixedDeltaTime;
            if (!Prototype.noActionMaximumDurationLimit && currentStageDuration >= stageMaximumDuration)
            {
                // abandon current progress, restart
                var currentNode = CurrentStage.Node;
                Restart();
                Log($"Behaviour Tree waiting for node {currentNode?.name ?? "None"} too long. The tree has restarted.");
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





        public bool IsInSubTreeOf(TreeNode parent, TreeNode child)
        {
            if (parent == null)
            {
                return false;
            }
            if (parent == child)
            {
                return true;
            }
            if (parent.IsParentOf(child))
            {
                return true;
            }
            List<NodeReference> list = parent.GetChildrenReference();
            for (int i = 0; i < list.Count; i++)
            {
                NodeReference item = list[i];
                var instance = GetNode(item);
                if (instance != null && IsInSubTreeOf(instance, child))
                {
                    return true;
                }
            }
            return false;
        }






        private void Log(object message)
        {
            if (debug) Debug.Log(message.ToString());
        }

        private void Try(System.Action action)
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, gameObject);
                switch (Prototype.treeErrorHandle)
                {
                    case BehaviourTreeErrorSolution.Pause:
                        Pause();
                        break;
                    case BehaviourTreeErrorSolution.Restart:
                        Restart();
                        break;
                    case BehaviourTreeErrorSolution.Throw:
                        throw;
                }
            }
        }






        /// <summary>
        /// generate the reference table of the behaviour tree
        /// </summary>
        /// <param name="nodes"></param>
        /// <exception cref="InvalidBehaviourTreeException">if behaviour tree data is invalid</exception>
        private void GenerateNodeReferenceTable()
        {
            foreach (var node in Prototype.nodes)
            {
                if (node is null)
                {
                    throw new InvalidBehaviourTreeException("A null node present in the behaviour tree, check your behaviour tree data.");
                }
                TreeNode newInstance = NodeFactory.Instantiate(node);
                newInstance.SetPrototype(node);
                references[newInstance.uuid] = newInstance;
            }
            //for node's null reference
            references[UUID.Empty] = null;
        }

        private void GenerateVariableTable()
        {
            foreach (var item in Prototype.variables)
            {
                if (!item.IsValid) continue;
                if (item.IsStatic) AddStaticVariable(item);
                else AddLocalVariable(item);
            }
            AddLocalVariable(VariableData.GetGameObjectVariable()).SetValue(attachedGameObject);
            AddLocalVariable(VariableData.GetTransformVariable()).SetValue(attachedTransform);
            AddLocalVariable(VariableData.GetTargetScriptVariable(script ? script.GetType() : null)).SetValue(script);
        }

        /// <summary>
        /// Assemble the reference UUID in the behaviour tree
        /// </summary>
        /// <exception cref="InvalidBehaviourTreeException">if behaviour tree data is invalid</exception>
        private void AssembleReference()
        {
            foreach (var node in references.Values)
            {
                // a empty reference
                if (node is null) continue;
                // unreachable node
                if (!references.ContainsKey(node.parent) && node != head) continue;

                LinkReference(node);
            }
        }

        private void InitializeNodes()
        {
            foreach (var node in references.Values)
            {
                // a empty reference
                if (node is null) continue;
                // unreachable node
                if (!references.ContainsKey(node.parent) && node != head) continue;
                node.Initialize();
            }
        }

        /// <summary>
        /// set links of tree node
        /// </summary>
        /// <param name="node">The tree node to be filled</param>
        private void LinkReference(TreeNode node)
        {
            node.behaviourTree = this;
            GetNode(node.parent);
            var serviceReferences = node.GetServices();
            if (serviceReferences != null)
            {
                for (int i = 0; i < serviceReferences.Count; i++)
                {
                    NodeReference serviceReference = serviceReferences[i];
                    TreeNode? serviceNodeInstance = GetNode(serviceReference);
                    if (!serviceReference.HasReference || serviceNodeInstance == null)
                    {
                        Debug.LogError($"Null Reference Service is found in node {node.name}({node.uuid})");
                        serviceReferences.RemoveAt(i);
                        i--;
                        continue;
                    }

                    serviceNodeInstance.parent = node;
                }
            }
            var accessor = NodeAccessorProvider.GetAccessor(node.GetType());
            foreach (var field in accessor.Variables)
            {
                var reference = field.Get(node);
                InitialzeVariable(reference);
            }
            foreach (var item in accessor.VariableCollections)
            {
                var reference = item.Get(node);
                if (reference == null) continue;
                foreach (var element in reference)
                {
                    if (element is not IVariableField variableField) continue;
                    InitialzeVariable(variableField);
                }
            }
        }

        private void InitialzeVariable(IVariableField reference)
        {
            if (reference == null) return;
            if (!reference.IsConstant) SetVariableFieldReference(reference.UUID, reference);
        }

        internal void GetNode<T>(ref T reference) where T : INodeReference, new()
        {
            reference ??= new();
            if (references.TryGetValue(reference.UUID, out var node)) reference.Set(node);
            else reference.Set(null);
        }


        internal TreeNode? GetNode(INodeReference? reference)
        {
            if (reference == null)
                return null;

            // try return cached node
            if (reference.Node != null)
                return reference.Node;

            // read node
            if (references.TryGetValue(reference.UUID, out var node)) reference.Set(node);
            else reference.Set(null);

            return node;
        }











        private VariableTable GetStaticVariableTable()
        {
            if (staticVariablesDictionary.TryGetValue(Prototype, out var table))
            {
                return table;
            }
            return staticVariablesDictionary[Prototype] = new VariableTable();
        }

        private void SetVariableFieldReference(UUID uuid, IVariableField clone)
        {
            //try get field
            bool hasVar = Variables.TryGetValue(uuid, out Variable variable);
            if (!hasVar) hasVar = StaticVariables.TryGetValue(uuid, out variable);
            if (!hasVar) hasVar = GlobalVariables.TryGetValue(uuid, out variable);

            //get variable, if exist, then set reference to a variable, else set to null
            if (hasVar) clone.SetRuntimeReference(variable);
            else clone.SetRuntimeReference(null);
        }

        private Variable AddLocalVariable(VariableData data)
        {
            if (!variables.TryGetValue(data.UUID, out var variable))
            {
                if (variableTranslations != null)
                {
                    variable = variableTranslations.GetVariable(data.UUID);
                }
                variable ??= VariableUtility.Create(data, script);

                // if translated, the variable could have different uuid link to the same variable data
                variables[data.UUID] = variable;
                variables[variable.UUID] = variable;
                return variable;
            }

            return variable;
        }

        private Variable AddStaticVariable(VariableData data)
        {
            // already initialized, return the variable
            if (StaticVariables.TryGetValue(data.UUID, out var staticVar))
                return staticVar;

            staticVar = new TreeVariable(data, true);
            return StaticVariables[data.UUID] = staticVar;
        }

        internal Variable GetVariable(UUID uuid)
        {
            bool found = variables.TryGetValue(uuid, out Variable v);
            if (found) return v;
            found = staticVariables.TryGetValue(uuid, out v);
            if (found) return v;
            GlobalVariables.TryGetValue(uuid, out v);
            return v;
        }

        internal bool TryGetVariable(UUID uuid, out Variable variable)
        {
            bool found;
            found = GlobalVariables.TryGetValue(uuid, out variable);
            if (found) return true;
            found = staticVariables.TryGetValue(uuid, out variable);
            if (found) return true;
            found = variables.TryGetValue(uuid, out variable);
            return found;
        }

        /// <summary>
        /// set variable's value by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetVariable<T>(string name, T value)
        {
            if (Variables.TryGetValue(name, out Variable variable))
            {
                variable?.SetValue(value);
                return true;
            }
            else if (StaticVariables.TryGetValue(name, out variable))
            {
                variable?.SetValue(value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// set variable's value by name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetVariable(string name, object value)
        {
            if (Variables.TryGetValue(name, out Variable variable))
            {
                variable?.SetValue(value);
                return true;
            }
            else if (StaticVariables.TryGetValue(name, out variable))
            {
                variable?.SetValue(value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// set variable's value by uuid
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool SetVariable(UUID uuid, object value)
        {
            if (Variables.TryGetValue(uuid, out Variable variable))
            {
                variable?.SetValue(value);
                return true;
            }
            else if (StaticVariables.TryGetValue(uuid, out variable))
            {
                variable?.SetValue(value);
                return true;
            }
            return false;
        }




        /// <summary>
        /// init the global variables from the AI Setting file
        /// </summary>
        /// <returns></returns>
        private static VariableTable BuildGlobalVariables()
        {
            var setting = AISetting.Instance;
            VariableTable globalVariables = new();

            if (setting == null) return globalVariables;
            foreach (var item in setting.globalVariables)
            {
                if (!item.IsValid) continue;

                TreeVariable variable = new(item, true);
                globalVariables[item.UUID] = variable;
                //if (AIGlobalVariableInitAttribute.GetInitValue(item.name, out var value)){  variable.SetValue(value); }
            }
            return globalVariables;
        }

        public static bool SetGlobalVariable(string name, object value)
        {
            if (GlobalVariables.TryGetValue(name, out var variable))
            {
                variable?.SetValue(value);
                return true;
            }
            return false;
        }

        public static bool SetGlobalVariable<T>(string name, T value)
        {
            if (GlobalVariables.TryGetValue(name, out var variable))
            {
                variable?.SetValue(value);
                return true;
            }
            return false;
        }

#if UNITY_EDITOR
        private void RecordStackEvent(NodeCallStack stack, NodeCallStack.EventType eventType, TreeNode node, State? result, string detail)
        {
            if (stackEvents.Count >= stackEventCapacity)
            {
                stackEvents.Dequeue();
            }

            activeStacks.TryGetValue(stack, out var metadata);
            stackEvents.Enqueue(new StackEventRecord(
                Time.frameCount,
                Time.realtimeSinceStartup,
                Prototype ? Prototype.name : "Behaviour Tree",
                metadata.DebugId,
                metadata.Type,
                string.IsNullOrWhiteSpace(metadata.Label) ? "Unknown Stack" : metadata.Label,
                eventType,
                node,
                result,
                stack?.State ?? NodeCallStack.StackState.Invalid,
                detail));
        }

        internal void ClearStackEvents()
        {
            stackEvents.Clear();
        }

        internal readonly struct StackEventRecord
        {
            public readonly int Frame;
            public readonly float Time;
            public readonly string TreeName;
            public readonly int StackId;
            public readonly StackType StackType;
            public readonly string StackName;
            public readonly NodeCallStack.EventType EventType;
            public readonly TreeNode Node;
            public readonly string NodeName;
            public readonly string NodeType;
            public readonly UUID NodeUUID;
            public readonly State? Result;
            public readonly NodeCallStack.StackState StackState;
            public readonly string Detail;

            public StackEventRecord(
                int frame,
                float time,
                string treeName,
                int stackId,
                StackType stackType,
                string stackName,
                NodeCallStack.EventType eventType,
                TreeNode node,
                State? result,
                NodeCallStack.StackState stackState,
                string detail)
            {
                Frame = frame;
                Time = time;
                TreeName = treeName;
                StackId = stackId;
                StackType = stackType;
                StackName = stackName;
                EventType = eventType;
                Node = node;
                NodeName = string.IsNullOrWhiteSpace(node?.name) ? "(null)" : node.name;
                NodeType = node?.GetType().Name ?? "(null)";
                NodeUUID = node?.uuid ?? UUID.Empty;
                Result = result;
                StackState = stackState;
                Detail = detail ?? string.Empty;
            }
        }
#endif



        public struct ExecutingNodeInfo
        {
            public TreeNode? Node { get; private set; }
            public float Duration { get; private set; }
            public float MaximumDuration { get; private set; }
            public readonly float RemainingDuration => MaximumDuration - Duration;
            public readonly string name => Node?.name ?? "None";

            public ExecutingNodeInfo(TreeNode? node, float duration, float maximumDuration)
            {
                Node = node;
                Duration = duration;
                MaximumDuration = maximumDuration;
            }
        }

        /// <summary>
        /// Collects running subtree nodes from the provided behaviour tree.
        /// </summary>
        /// <param name="tree">The behaviour tree to scan.</param>
        /// <param name="subtrees">The list that receives subtree nodes.</param>
        /// <param name="visited">The set of visited behaviour trees to prevent cycles.</param>
        /// <returns>No return value.</returns>
        private static void CollectRunningSubtrees(BehaviourTree tree, List<Subtree> subtrees, HashSet<BehaviourTree> visited)
        {
            if (tree == null || subtrees == null || visited == null)
            {
                return;
            }

            if (!visited.Add(tree))
            {
                return;
            }

            if (tree.references == null || tree.mainStack == null)
            {
                return;
            }

            foreach (var stack in tree.GetActiveStacksSnapshot())
            {
                var node = stack.Current;
                if (node is not Subtree subtree || subtree.RuntimeTree == null)
                {
                    continue;
                }

                subtrees.Add(subtree);
                CollectRunningSubtrees(subtree.RuntimeTree, subtrees, visited);
            }
        }
    }
}
