using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using Minerva.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Amlos.AI
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

        private static VariableTable globalVariables;
        private static readonly Dictionary<BehaviourTreeData, VariableTable> staticVariablesDictionary = new();


        private const float defaultActionMaximumDuration = 60f;

        //internal event UpdateDelegate UpdateCall;
        //internal event UpdateDelegate LateUpdateCall;
        //internal event UpdateDelegate FixedUpdateCall;


        private readonly GameObject attachedGameObject;
        private readonly Transform attachedTransform;
        private readonly Dictionary<UUID, TreeNode> references;
        private readonly VariableTable variables;
        private readonly VariableTable staticVariables;
        private readonly Task initer;
        private readonly MonoBehaviour script;
        private readonly AI ai;

        [SerializeField] private bool debug = false;
        private TreeNode head;
        private float stageMaximumDuration;
        private Dictionary<TreeNode, NodeCallStack> serviceStacks;
        private NodeCallStack mainStack;
        private float currentStageDuration;

        /// <summary> How long is current stage? </summary>
        public float CurrentStageDuration => currentStageDuration;
        public bool IsInitialized => initer != null && initer.IsCompletedSuccessfully;
        public bool IsError => initer.IsFaulted || initer.IsCanceled;
        public bool IsRunning => mainStack?.IsRunning == true;
        public bool Debugging { get => debug; set { debug = value; } }
        /// <summary> Stop if main stack is set to pause  </summary>
        public bool IsPaused => IsRunning && (mainStack?.IsPaused == true);
        public TreeNode Head => head;
        public MonoBehaviour Script => script;
        public GameObject gameObject => attachedGameObject;
        public AI AIComponent => ai;
        public Transform transform => attachedTransform;
        public Dictionary<UUID, TreeNode> References => references;
        internal VariableTable Variables => variables;
        internal VariableTable StaticVariables => staticVariables;
        public BehaviourTreeData Prototype { get; private set; }
        public NodeCallStack MainStack => mainStack;
        public Dictionary<TreeNode, NodeCallStack> ServiceStacks => serviceStacks;
        public TreeNode ExecutingNode => mainStack?.Current;
        public TreeNode LastExecutedNode => mainStack?.Previous;
        public ExecutingNodeInfo CurrentStage => new(mainStack?.Current, currentStageDuration, stageMaximumDuration);


        private bool CanContinue => IsRunning && (mainStack?.IsPaused == false);
        /// <summary>
        /// Global variables of the behaviour tree
        /// <br/>
        /// (The variable shared in all behaviour tree)
        /// </summary>
        internal static VariableTable GlobalVariables => globalVariables ??= BuildGlobalVariables();

        #region Editor
#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY
        /// <br/>
        /// Variable table of the behaviour tree
        /// </summary>
        public VariableTable EditorVariables => Variables;
        /// <summary>
        /// EDITOR ONLY
        /// <br/>
        /// Static variable table of the behaviour tree
        /// </summary>
        public VariableTable EditorStaticVariables => StaticVariables;
        /// <summary>
        /// EDITOR ONLY
        /// <br/>
        /// Gloabl variable table of the behaviour tree
        /// </summary>
        public static VariableTable EditorGlobalVariables => GlobalVariables;
#endif

        #endregion



        public BehaviourTree(BehaviourTreeData behaviourTreeData, GameObject gameObject, MonoBehaviour script)
        {
            this.Prototype = behaviourTreeData;
            this.script = script;
            this.attachedGameObject = gameObject;
            this.attachedTransform = gameObject.transform;
            this.ai = gameObject.GetComponent<AI>();

            if (!script) Debug.LogWarning("No control script assigned to AI", attachedGameObject);

            references = new();
            serviceStacks = new();
            variables = new VariableTable(true);
            staticVariables = GetStaticVariableTable();
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
            InitializationTask(behaviourTreeData);
#elif UNITY_2023_1_OR_NEWER
                await Awaitable.BackgroundThreadAsync();
                InitializationTask(behaviourTreeData);
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

        private void InitializationTask(BehaviourTreeData behaviourTreeData)
        {
            GenerateReferenceTable();

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
            mainStack = new NodeCallStack();
            mainStack.OnNodePopStack += RemoveServicesRegistry;

            serviceStacks = new();
            serviceStacks.Clear();

            mainStack.Initialize();
            RegistryServices(head);
            ResetStageTimer();
            mainStack.Start(head);
        }




        /// <summary>
        /// Add node to the progress stack
        /// </summary>
        /// <param name="nodeReference"></param>
        /// <param name="callStack"></param>
        internal void ExecuteNext(NodeReference nodeReference, NodeCallStack callStack)
        {
            var node = GetNode(nodeReference);
            if (node is null)
            {
                HandleNullNode();
                return;
            }

            callStack.Push(node);
            RegistryServices(node);

            if (callStack == mainStack)
            {
                ResetStageTimer();
            }
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
            //trying to end other node
            if (mainStack.Current == treeNode) return true;
            foreach (var item in serviceStacks)
            {
                if (item.Value.Current == treeNode) return true;
            }
            return false;
        }

        private NodeCallStack GetServiceStack(TreeNode node)
        {
            if (node == null)
            {
                return null;
            }

            return serviceStacks.TryGetValue(node, out var stack) ? stack : null;
        }

        private void RegistryServices(TreeNode node)
        {
            foreach (var item in node.services)
            {
                if (GetNode(item) is not Service service)
                {
                    Debug.LogError($"Invalid service reference on node [{node.name}].");
                    continue;
                }

                if (serviceStacks.ContainsKey(service))
                {
                    continue;
                }

                var serviceStack = new NodeCallStack();
                serviceStack.OnNodePopStack += RemoveServicesRegistry;
                serviceStacks[service] = serviceStack;
                service.OnRegistered();
            }
        }

        private void RemoveServicesRegistry(TreeNode node)
        {
            foreach (var item in node.services)
            {
                if (GetNode(item) is not Service service)
                {
                    continue;
                }
                // service might have been remove early
                if (!serviceStacks.ContainsKey(service))
                {
                    continue;
                }
                var stack = serviceStacks[service];
                stack.End();
                serviceStacks.Remove(service);
                service.OnUnregistered();
            }
            ResetStageTimer();
        }

        private void RunService(Service service, NodeCallStack stack)
        {
            //last service hasn't finished 
            if (stack.Count != 0)
            {
                Log($"Service {service.name} did not finish executing in expect time.");
                stack.End();
            }

            //execute
            stack.Initialize();
            RegistryServices(service);
            stack.Start(service);
            //Debug.Log("Service Complete");
        }

        /// <summary>
        /// end a service
        /// </summary>
        /// <param name="service"></param>
        internal void EndService(Service service)
        {
            var stack = GetServiceStack(service) ?? throw new ArgumentException("Given service does not exist in stacks", nameof(service));
            stack.End();
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

            mainStack.End();
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
            AssembleReference();
            InitializeNodes();
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

            Try(mainStack.Update);
            foreach (var stack in ServiceStacks)
            {
                Try(stack.Value.Update);
            }

        }

        /// <summary>
        /// LateUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        internal void LateUpdate()
        {
            //don't update when paused
            if (!CanContinue) return;

            Try(mainStack.LateUpdate);
            foreach (var stack in ServiceStacks)
            {
                Try(stack.Value.LateUpdate);
            }

        }

        /// <summary>
        /// FixedUpdate of behaviour tree, called every frame in the <see cref="AI"/>
        /// </summary>
        internal void FixedUpdate()
        {
            //don't update when paused  
            if (!CanContinue) return;
            Try(mainStack.FixedUpdate);

            if (!CanContinue) return;
            Try(ServiceUpdate);

            if (!CanContinue) return;
            foreach (var stack in ServiceStacks)
            {
                if (!CanContinue) return;
                Try(stack.Value.FixedUpdate);
            }

            if (!CanContinue) return;
            RunStageTimer();
        }




        /// <summary>
        /// Service update (during fixed update)
        /// </summary>
        private void ServiceUpdate()
        {
            //Debug.Log("Service Update Start :" + mainStack);
            var stacks = GetActiveStacksSnapshot();
            for (int i = 0; i < stacks.Count; i++)
            {
                var callStack = stacks[i];
                if (callStack?.Nodes == null)
                {
                    continue;
                }

                var stackNodes = callStack.Nodes.ToArray();
                for (int j = 0; j < stackNodes.Length; j++)
                {
                    TreeNode progress = stackNodes[j];
                    if (progress?.services == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < progress.services.Count; k++)
                    {
                        var node = GetNode(progress.services[k]);
                        if (node is not Service service)
                        {
                            continue;
                        }

                        //service not found
                        if (!serviceStacks.TryGetValue(service, out var serviceStack))
                        {
                            //Log($"Service {service.name} did not load into the behaviour tree properly.");
                            continue;
                        }
                        //Log($"Service {service.name} Start");

                        //increase service timer
                        //serviceStack.currentFrame++;
                        service.UpdateTimer();
                        if (!service.IsReady) continue;

                        RunService(service, serviceStack);
                    }
                }
            }
        }

        /// <summary>
        /// Create a snapshot of all active call stacks for service scheduling.
        /// </summary>
        /// <returns>A list containing the main stack and any active service stacks.</returns>
        /// <exception cref="System.Exception">No exceptions are thrown by this method.</exception>
        private List<NodeCallStack> GetActiveStacksSnapshot()
        {
            var stacks = new List<NodeCallStack>();
            if (mainStack != null)
            {
                stacks.Add(mainStack);
            }

            if (serviceStacks != null)
            {
                stacks.AddRange(serviceStacks.Values.Where(stack => stack != null));
            }

            return stacks;
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
                Log($"Behaviour Tree waiting for node {currentNode.name} too long. The tree has restarted.");
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
                if (IsInSubTreeOf(instance, child))
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
        private void GenerateReferenceTable()
        {
            IEnumerable<TreeNode> nodes = Prototype.GetNodesCopy();
            foreach (var node in nodes)
            {
                if (nodes is null)
                {
                    throw new InvalidBehaviourTreeException("A null node present in the behaviour tree, check your behaviour tree data.");
                }
                TreeNode newInstance = node.Clone();
                references[newInstance.uuid] = newInstance;
            }
            foreach (var item in Prototype.variables)
            {
                if (!item.IsValid) continue;
                if (item.IsStatic) AddStaticVariable(item);
                else AddLocalVariable(item);
            }
            AddLocalVariable(VariableData.GetGameObjectVariable()).SetValue(attachedGameObject);
            AddLocalVariable(VariableData.GetTransformVariable()).SetValue(attachedTransform);
            AddLocalVariable(VariableData.GetTargetScriptVariable(script ? script.GetType() : null)).SetValue(script);
            //for node's null reference
            references[UUID.Empty] = null;
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
            references.TryGetValue(node.parent, out var parent);
            node.behaviourTree = this;
            node.parent = parent;
            node.services = node.services?.Select(u => { GetNode(u); return u; }).ToList() ?? new List<NodeReference>();
            for (int i = 0; i < node.services.Count; i++)
            {
                NodeReference serviceReference = node.services[i];
                if (!serviceReference.HasReference)
                {
                    Debug.LogError($"Null Reference Service is found in node {node.name}({node.uuid})");
                    node.services.RemoveAt(i);
                    i--;
                    continue;
                }
                TreeNode serviceNodeInstance = GetNode(serviceReference);
                serviceNodeInstance.parent = node;
            }
            //foreach (var field in node.GetType().GetFields())
            //{
            //    if (field.FieldType.IsSubclassOf(typeof(VariableBase)))
            //    {
            //        var reference = (VariableBase)field.GetValue(node);
            //        if (!reference.IsConstant) SetVariableFieldReference(reference.UUID, reference);
            //        else if (reference.Type == VariableType.UnityObject) SetVariableFieldReference(reference.ConstanUnityObjectUUID, reference);
            //    }
            //}
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
                    if (element is not VariableBase variableBase) continue;
                    InitialzeVariable(variableBase);
                }
            }
        }

        private void InitialzeVariable(VariableBase reference)
        {
            if (reference == null) return;
            if (!reference.IsConstant) SetVariableFieldReference(reference.UUID, reference);
            else if (reference.Type == VariableType.UnityObject) SetVariableFieldReference(reference.ConstanUnityObjectUUID, reference);
        }

        internal void GetNode<T>(ref T reference) where T : INodeReference, new()
        {
            reference ??= new();
            if (references.TryGetValue(reference.UUID, out var node)) reference.Set(node);
            else reference.Set(null);
        }


        internal TreeNode GetNode(INodeReference reference)
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

        private void SetVariableFieldReference(UUID uuid, VariableBase clone)
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
            var localVar = VariableUtility.Create(data, script);
            variables[data.UUID] = localVar;
            return localVar;
        }

        private Variable AddStaticVariable(VariableData data)
        {
            //initialized already
            if (StaticVariables.TryGetValue(data.UUID, out var staticVar)) return staticVar;

            staticVar = new TreeVariable(data, true);
            return StaticVariables[data.UUID] = staticVar;
        }

        private Variable AddStaticVariable(AssetReferenceData data)
        {
            if (StaticVariables.TryGetValue(data.UUID, out var staticVar)) return staticVar;
            staticVar = new TreeVariable(data);
            return StaticVariables[data.UUID] = staticVar;
        }

        internal Variable GetVariable(UUID uuid)
        {
            bool found = variables.TryGetValue(uuid, out Variable v);
            if (found) return v;
            found = staticVariables.TryGetValue(uuid, out v);
            if (found) return v;
            globalVariables.TryGetValue(uuid, out v);
            return v;
        }

        internal bool TryGetVariable(UUID uuid, out Variable variable)
        {
            bool found;
            found = globalVariables.TryGetValue(uuid, out variable);
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



        public struct ExecutingNodeInfo
        {
            public TreeNode Node { get; private set; }
            public float Duration { get; private set; }
            public float MaximumDuration { get; private set; }
            public readonly float RemainingDuration => MaximumDuration - Duration;
            public readonly string name => Node?.name ?? "None";

            public ExecutingNodeInfo(TreeNode node, float duration, float maximumDuration)
            {
                Node = node;
                Duration = duration;
                MaximumDuration = maximumDuration;
            }
        }
    }
}
