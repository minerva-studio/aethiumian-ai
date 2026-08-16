using Aethiumian.AI.Attributes;
using System.Linq;
using System.Collections.Generic;
using System;
using UnityEngine;
using Aethiumian.AI.Variables;
using Aethiumian.AI.References;
using Aethiumian.AI.Nodes;
using UnityEngine.Serialization;
using Aethiumian.AI.Accessors;
using Aethiumian.AI.Randomization;

#if UNITY_EDITOR 
using UnityEditor;
#endif

namespace Aethiumian.AI
{
    /// <summary>
    /// Data asset of the behaviour tree
    /// <br/>
    /// Author: Wendell 
    /// </summary>
    [CreateAssetMenu(fileName = "AI_NAME", menuName = "Aethiumian AI/Behaviour Tree")]
    public partial class BehaviourTreeData : ScriptableObject
    {
        [Header("Settings")]
        public bool noActionMaximumDurationLimit;
        public float actionMaximumDuration = 60;
        [FormerlySerializedAs("errorHandle")]
        public BehaviourTreeErrorSolution treeErrorHandle;
        public NodeErrorSolution nodeErrorHandle;
        public RandomSourceBinding randomSource = RandomSourceBinding.WithScope(RandomSourceScope.Local);

        [Header("Content")]
        public UUID headNodeUUID;
        [SerializeReference]
        public List<TreeNode> nodes = new();
        public List<VariableData> variables = new();



        /// <summary>
        /// Get a copy of all nodes in behaviour tree
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TreeNode> GetNodesCopy()
        {
            return nodes.Select(NodeFactory.Duplicate);
        }

        /// <summary>
        /// Self-check whether the behaviour tree data is invalid
        /// </summary>
        /// <returns></returns>
        public bool IsInvalid()
        {
            return nodes.Any(s => s == null);
        }

#if UNITY_EDITOR
        public MonoScript targetScript;
        public GameObject prefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [HideInInspector][SerializeReference] private GraphLayoutData graphLayout;
        private Dictionary<UUID, TreeNode> dictionary;

        SerializedObject serializedObject;
        SerializedProperty nodeList;

        /// <summary>
        /// EDITOR ONLY<br/>
        /// Optimization UUID-TreeNode dictionary
        /// </summary>
        public Dictionary<UUID, TreeNode> Dictionary { get => dictionary ??= GenerateTable(); }
        public TreeNode Head => GetNode(headNodeUUID);
        public IReadOnlyList<TreeNode> EditorNodes => nodes;
        public IReadOnlyCollection<VariableData> EditorVariables => GetVariables();
        /// <summary>
        /// Gets the optional native graph layout without creating or dirtying the asset.
        /// </summary>
        internal GraphLayoutData GraphLayout
        {
            get => graphLayout;
            set => graphLayout = value;
        }
        public SerializedObject SerializedObject { get { return serializedObject ??= new SerializedObject(this); } }
        public RuntimeAnimatorController BaseAnimatorController { get => animatorController; set => animatorController = value; }
        public UnityEditor.Animations.AnimatorController AnimatorController
        {
            get
            {
                return animatorController switch
                {
                    UnityEditor.Animations.AnimatorController controller => controller,
                    AnimatorOverrideController overrideController => overrideController.runtimeAnimatorController as UnityEditor.Animations.AnimatorController,
                    _ => null,
                };
            }
        }


        public SerializedProperty GetNodeProperty(TreeNode node)
        {
            int index = nodes.IndexOf(node);
            if (index == -1) return null;
            nodeList ??= SerializedObject.FindProperty(nameof(nodes));
            return nodeList.arraySize <= index ? null : nodeList.GetArrayElementAtIndex(index);
        }

        public HashSet<VariableData> GetVariables()
        {
            var list = new HashSet<VariableData>();
            list.UnionWith(variables);
            return list;
        }

        /// <summary>
        /// Validates that every authored structural node reference forms one strict parent-child tree edge.
        /// Raw references and non-node data references are intentionally excluded.
        /// </summary>
        /// <returns>Human-readable errors describing every conflicting structural relationship.</returns>
        public IReadOnlyList<string> GetStructureValidationErrors()
        {
            return NodeTopologySnapshot.Create(nodes).GetValidationErrors();
        }


        /// <summary>
        /// EDITOR ONLY <br/>
        /// traverse the tree, and return all nodes that is in the tree
        /// <para>if the node is unreachable, it will not shown in the tree</para>
        /// </summary>
        /// <returns></returns>
        public List<TreeNode> Traverse()
        {
            Stack<TreeNode> stack = new Stack<TreeNode>();
            List<TreeNode> result = new List<TreeNode>();
            stack.Push(Head);
            TreeNode current;

            while (stack.Count != 0)
            {
                current = stack.Pop();
                List<NodeReference> children = current.GetChildrenReference();
                if (children is null) continue;
                foreach (var item in children)
                {
                    var node = GetNode(item);
                    if (node != null && !result.Contains(node))
                    {
                        result.Add(node);
                        stack.Push(node);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Regenerate the uuid-TreeNode table
        /// </summary>
        public void RegenerateTable()
        {
            dictionary = GenerateTable();
            serializedObject?.Update();
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// generate the uuid-TreeNode table
        /// </summary>
        /// <returns></returns>
        private Dictionary<UUID, TreeNode> GenerateTable()
        {
            return nodes.Where(n => null != n).ToDictionary(n => n.uuid);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get a node by uuid
        /// </summary>
        /// <param name="uUID"></param>
        /// <returns></returns>
        public TreeNode GetNode(UUID uUID)
        {
            try
            {
                return Dictionary.TryGetValue(uUID, out var value) ? value : null;
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get a node by uuid
        /// </summary>
        /// <param name="uUID"></param>
        /// <returns></returns>
        public TreeNode GetParent(TreeNode node)
        {
            if (node == null) return null;
            return GetNode(node.parent);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Check a node is in a service call
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool IsServiceCall(TreeNode node)
        {
            return GetServiceHead(node) != null;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get Service head of a service branch
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Service GetServiceHead(TreeNode node)
        {
            if (node is null)
            {
                return null;
            }

            TreeNode current = node;
            TreeNode parentNode = GetParent(current);
            while (parentNode != null)
            {
                if (parentNode.TryAsServiceHost(out var serviceHost) && TreeNode.IsListedAsService(serviceHost, current))
                {
                    return current as Service;
                }

                current = parentNode;
                parentNode = GetParent(current);
            }

            return null;
        }





        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable data by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(string varName)
        {
            if (varName == VariableData.GAME_OBJECT_VARIABLE_NAME)
            {
                return VariableData.GetGameObjectVariable();
            }
            else if (varName == VariableData.TRANSFORM_VARIABLE_NAME)
            {
                return VariableData.GetTransformVariable();
            }
            else if (varName == VariableData.TARGET_SCRIPT_VARIABLE_NAME)
            {
                return VariableData.GetTargetScriptVariable(targetScript.GetClass());
            }
            else
            {
                return EditorVariables.FirstOrDefault(v => v.name == varName);
            }
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(UUID uuid)
        {
            if (uuid == VariableData.localGameObject)
            {
                return VariableData.GetGameObjectVariable();
            }
            else if (uuid == VariableData.localTransform)
            {
                return VariableData.GetTransformVariable();
            }
            else if (uuid == VariableData.targetScript)
            {
                System.Type type = targetScript ? targetScript.GetClass() : null;
                return VariableData.GetTargetScriptVariable(type);
            }
            else
            {
                return EditorVariables.FirstOrDefault(v => v.UUID == uuid);
            }
        }

        /// <summary>
        /// Remove the variable
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public bool RemoveVariable(UUID uuid)
        {
            for (int i = 0; i < variables.Count; i++)
            {
                var v = variables[i];
                if (v.UUID == uuid)
                {
                    Undo.RecordObject(this, $"Remove Variable {v.name}");
                    variables.RemoveAt(i);
                    SerializedObject.Update();
                    return true;
                }
            }
            return false;
        }

        public void AddVariable(VariableData item, bool recordUndo = true)
        {
            if (item is null)
            {
                return;
            }

            if (recordUndo) Undo.RecordObject(this, $"Add variable {item.name} to {name}");
            variables.Add(item);
            SerializedObject.Update();
        }

        public System.Type GetVariableType(UUID uuid)
        {
            var variable = GetVariable(uuid);
            return variable?.ObjectType;
        }





        /// <summary>
        /// EDITOR ONLY <br/>
        /// Generate new name for new node
        /// </summary> 
        /// <returns></returns>
        public string GenerateNewNodeName(TreeNode node)
        {
            string wanted = node.GetType().Name;
            return GenerateNewNodeName(wanted);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Generate new name for new node
        /// </summary> 
        /// <returns></returns>
        public string GenerateNewNodeName(string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted))
                wanted = "Node";

            var match = System.Text.RegularExpressions.Regex.Match(wanted, @"^(.*?)(\s\d+)?$");
            string baseName = match.Groups[1].Value;

            if (!nodes.Any(n => n.name == wanted))
                return wanted;

            int i = 2;
            string newName;
            do
            {
                newName = $"{baseName} {i}";
                i++;
            }
            while (nodes.Any(n => n.name == newName));

            return newName;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Generate new name for new variable
        /// </summary> 
        /// <returns></returns>
        public string GenerateNewVariableName(string wanted)
        {
            if (!variables.Any(n => n.name == wanted))
            {
                return wanted;
            }

            int i = 2;
            while (true)
            {
                var newName = wanted + " " + i;
                if (!variables.Any(n => n.name == newName))
                {
                    return newName;
                }
                i++;
            }
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Create new variable
        /// </summary> 
        /// <param name="variableType">variable type</param>
        /// <returns></returns>
        public VariableData CreateNewVariable(VariableType variableType)
        {
            VariableData vData = new(name: GenerateNewVariableName(variableType.ToString()), variableType: variableType);
            variables.Add(vData);
            return vData;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Create new variable
        /// </summary> 
        /// <param name="variableType">variable type</param>
        /// <returns></returns>
        public VariableData CreateNewVariable(VariableType variableType, string name)
        {
            VariableData vData = new(name: name, variableType: variableType);
            variables.Add(vData);
            return vData;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Add tree node
        /// </summary> 
        /// <param name="node">variable type</param>
        /// <returns></returns>
        public void Add(TreeNode node, bool recordUndo = true)
        {
            if (node is null)
            {
                return;
            }

            if (recordUndo) Undo.RecordObject(this, $"Add node {node.name} to {name}");

            nodes.Add(node);
            Dictionary[node.uuid] = node;
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Add tree nodes
        /// </summary> 
        /// <param name="nodes">variable type</param>
        /// <returns></returns>
        public void AddRange(IEnumerable<TreeNode> nodes, bool recordUndo = true)
        {
            if (nodes == null) return;
            if (recordUndo) Undo.RecordObject(this, $"Add {nodes.Count()} node to {name}");

            foreach (var node in nodes)
            {
                Add(node, false);
            }
        }


        /// <summary>
        /// EDITOR ONLY <br/>
        /// remove the node from the tree
        /// </summary>
        /// <param name="node"></param>
        public void Remove(TreeNode node, bool recordUndo = true)
        {
            if (node == null || !nodes.Contains(node))
            {
                return;
            }

            RemoveNodes(node.uuid, $"Remove node {node.name} from {name}", recordUndo);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// remove the node and the subtree under the node from the tree
        /// </summary>
        /// <param name="node"></param>
        public void RemoveSubTree(TreeNode node, bool recordUndo = true)
        {
            if (node == null || !nodes.Contains(node))
            {
                return;
            }

            HashSet<UUID> removedUUIDs = new();
            Stack<TreeNode> pending = new();
            pending.Push(node);
            while (pending.Count > 0)
            {
                TreeNode current = pending.Pop();
                if (current == null || !removedUUIDs.Add(current.uuid))
                {
                    continue;
                }

                foreach (NodeReference childReference in current.GetChildrenReference())
                {
                    TreeNode child = GetNode(childReference);
                    if (child != null && !removedUUIDs.Contains(child.uuid))
                    {
                        pending.Push(child);
                    }
                }
            }

            RemoveNodes(removedUUIDs, $"Remove node {node.name} from {name}", recordUndo);
        }

        /// <summary>
        /// Removes one authored UUID set and clears every incoming reference before committing the mutation.
        /// </summary>
        /// <param name="removedNodes">The nodes to remove.</param>
        /// <param name="undoName">The single Undo operation name.</param>
        /// <param name="recordUndo">Whether to record one Undo operation.</param>
        private void RemoveNodes(RemovedNodeSet removedNodes, string undoName, bool recordUndo)
        {
            if (removedNodes.Count == 0)
            {
                return;
            }

            HashSet<UUID> removedUUIDs = removedNodes.IsSingle
                ? new HashSet<UUID> { removedNodes.SingleUUID }
                : new HashSet<UUID>(removedNodes.UUIDs);
            TryDeleteNodes(removedUUIDs, undoName, recordUndo);
        }

        /// <summary>
        /// Provides allocation-free membership checks for one node and hash-based checks for subtree deletion.
        /// </summary>
        private readonly struct RemovedNodeSet
        {
            private readonly UUID singleUUID;
            private readonly HashSet<UUID> uuids;
            private readonly bool isSingle;

            /// <summary>Creates a single-node removal set without allocating a collection.</summary>
            /// <param name="uuid">The node UUID to remove.</param>
            internal RemovedNodeSet(UUID uuid)
            {
                singleUUID = uuid;
                uuids = null;
                isSingle = true;
            }

            /// <summary>Wraps the UUID set collected for subtree removal.</summary>
            /// <param name="uuids">The collected subtree UUIDs.</param>
            internal RemovedNodeSet(HashSet<UUID> uuids)
            {
                singleUUID = UUID.Empty;
                this.uuids = uuids;
                isSingle = false;
            }

            internal int Count => isSingle ? 1 : uuids?.Count ?? 0;
            internal bool IsSingle => isSingle;
            internal UUID SingleUUID => singleUUID;
            internal ISet<UUID> UUIDs => uuids;

            /// <summary>Checks whether the removal set contains one UUID.</summary>
            /// <param name="uuid">The UUID to check.</param>
            /// <returns><c>true</c> when the UUID is scheduled for removal.</returns>
            internal bool Contains(UUID uuid) => isSingle ? uuid == singleUUID : uuids?.Contains(uuid) == true;

            public static implicit operator RemovedNodeSet(UUID uuid) => new RemovedNodeSet(uuid);
            public static implicit operator RemovedNodeSet(HashSet<UUID> uuids) => new RemovedNodeSet(uuids);
        }

        public void Relink()
        {
            RegenerateTable();
            ReconcileUnambiguousParents();
        }

        /// <summary>
        /// Repairs only parent metadata that has an unambiguous authored owner.
        /// Multiple incoming edges and cycles are left untouched for explicit diagnosis.
        /// </summary>
        /// <returns>Validation errors that remain after the repair attempt.</returns>
        public IReadOnlyList<string> RepairParentMetadata()
        {
            NodeTopologySnapshot topology = NodeTopologySnapshot.Create(nodes);
            List<(TreeNode Node, UUID ParentUUID)> repairs = new();
            foreach (TreeNode node in nodes.Where(node => node != null))
            {
                IReadOnlyList<NodeReferenceOccurrence> incoming = topology.GetIncoming(node);
                if (incoming.Count != 1 || (node.parent?.UUID ?? UUID.Empty) == incoming[0].Owner.uuid)
                {
                    continue;
                }

                repairs.Add((node, incoming[0].Owner.uuid));
            }

            if (repairs.Count > 0)
            {
                Undo.RecordObject(this, "Repair node parent metadata");
                foreach ((TreeNode node, UUID parentUUID) in repairs)
                {
                    node.parent = new NodeReference(parentUUID);
                }

                SerializedObject.Update();
                SerializedObject.ApplyModifiedProperties();
                SerializedObject.Update();
                RegenerateTable();
                EditorUtility.SetDirty(this);
            }

            return NodeTopologySnapshot.Create(nodes).GetValidationErrors();
        }

        public string GetVariableDescName(UUID uuid)
        {
            if (uuid == UUID.Empty)
            {
                return VariableData.NONE_VARIABLE_NAME;
            }
            return GetVariable(uuid)?.GetDescriptiveName() ?? VariableData.MISSING_VARIABLE_NAME;
        }

        public string GetVariableDescName(VariableData data)
        {
            if (data == null)
            {
                return VariableData.NONE_VARIABLE_NAME;
            }
            return data.GetDescriptiveName();
        }
#endif
    }
}
