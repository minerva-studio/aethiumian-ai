using Minerva.Module;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Amlos.AI.Variables;
using Amlos.AI.References;
using Amlos.AI.Nodes;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEditor;
#endif

namespace Amlos.AI
{
    /// <summary>
    /// Data asset of the behaviour tree
    /// <br/>
    /// Author: Wendell 
    /// </summary>
    [CreateAssetMenu(fileName = "AI_NAME", menuName = "Library of Meialia/Entity/Behaviour Tree")]
    public class BehaviourTreeData : ScriptableObject
    {
        [Header("Settings")]
        public bool noActionMaximumDurationLimit;
        [DisplayIf(nameof(noActionMaximumDurationLimit), false)]
        public float actionMaximumDuration = 60;
        [FormerlySerializedAs("errorHandle")]
        public BehaviourTreeErrorSolution treeErrorHandle;
        public NodeErrorSolution nodeErrorHandle;

        [Header("Content")]
        public UUID headNodeUUID;
        [SerializeReference] public List<TreeNode> nodes = new();
        public List<VariableData> variables = new();
        public List<AssetReferenceData> assetReferences = new();


        /// <summary>
        /// Get a copy of all nodes in behaviour tree
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TreeNode> GetNodesCopy()
        {
            return nodes.Select(gn => gn.Clone());
        }

        /// <summary>
        /// Self-check whether the behaviour tree data is invalid
        /// </summary>
        /// <returns></returns>
        public bool IsInvalid()
        {
            return nodes.Any(s => s == null);
        }

        /// <summary>
        /// Get Asset by uuid
        /// </summary>
        /// <param name="assetReferenceUUID"></param>
        /// <returns></returns>
        public UnityEngine.Object GetAsset(UUID assetReferenceUUID)
        {
            assetReferences ??= new List<AssetReferenceData>();
            return assetReferences.FirstOrDefault(a => a.UUID == assetReferenceUUID)?.Asset;
        }

#if UNITY_EDITOR
        public MonoScript targetScript;
        public AnimatorController animatorController;
        public GameObject prefab;
        [HideInInspector][SerializeReference] private Graph graph = new Graph();
        private Dictionary<UUID, TreeNode> dictionary;
        SerializedObject serializedObject;
        /// <summary>
        /// EDITOR ONLY<br/>
        /// Optimization UUID-TreeNode dictionary
        /// </summary>
        public Dictionary<UUID, TreeNode> Dictionary { get => dictionary ??= GenerateTable(); }

        public TreeNode Head => GetNode(headNodeUUID);
        public List<TreeNode> AllNodes { get { return nodes; } }
        public Graph Graph { get => graph ??= new Graph(); set => graph = value; }
        public SerializedObject SerializedObject { get { return serializedObject ??= new SerializedObject(this); } }


        public SerializedProperty GetNodeProperty(TreeNode node)
        {
            int index = nodes.IndexOf(node);
            if (index == -1) return null;
            SerializedObject.Update();
            SerializedProperty serializedProperty = SerializedObject.FindProperty(nameof(nodes));
            return serializedProperty.arraySize <= index ? null : serializedProperty.GetArrayElementAtIndex(index);
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
            return Dictionary.TryGetValue(uUID, out var value) ? value : null;
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
            if (node is null) return false;
            if (node.parent == UUID.Empty)
            {
                return false;
            }
            if (node is Service)
            {
                return true;
            }
            return IsServiceCall(GetNode(node.parent));
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get Service head of a service branch
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public Service GetServiceHead(TreeNode node)
        {
            if (node is null) return null;
            if (node.parent == UUID.Empty)
            {
                return null;
            }
            if (node is Service s)
            {
                return s;
            }
            return GetServiceHead(GetNode(node.parent));
        }





        /// <summary>
        /// Whether asset data contain inside
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public bool HasAsset(UnityEngine.Object asset)
        {
            return assetReferences.Any(t => t.Asset == asset);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Add asset to behaviour tree
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public AssetReferenceData AddAsset(UnityEngine.Object asset, bool isFromVariable = false)
        {
            if (asset is MonoScript)
            {
                return null;
            }
            assetReferences ??= new List<AssetReferenceData>();
            AssetReferenceData assetReference = assetReferences.FirstOrDefault(a => a.Asset == asset);
            if (assetReference == null)
            {
                assetReference = new AssetReferenceData(asset);
                assetReferences.Add(assetReference);
            }
            assetReference.isFromVariable = isFromVariable;
            return assetReference;
        }

        public int RemoveAsset(UnityEngine.Object asset)
        {
            return assetReferences.RemoveAll(t => t.Asset == asset);
        }


        public int RemoveAsset(UUID uuid)
        {
            return assetReferences.RemoveAll(t => t.UUID == uuid);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Clear unused asset reference
        /// </summary> 
        /// <returns></returns>
        public void ClearUnusedAssetReference()
        {
            HashSet<UUID> used = new HashSet<UUID>();
            foreach (var item in AllNodes)
            {
                var fields = item.GetType().GetFields();
                foreach (var field in fields)
                {
                    //if (field.FieldType.IsSubclassOf(typeof(AssetReferenceBase)))
                    //{
                    //    var reference = field.GetValue(item) as AssetReferenceBase;
                    //    used.Add(reference.uuid);
                    //}
                    if (field.FieldType.IsSubclassOf(typeof(VariableBase)))
                    {
                        var variableField = field.GetValue(item) as VariableBase;
                        if (variableField.IsConstant && variableField.Type == VariableType.UnityObject)
                        {
                            used.Add(variableField.ConstanUnityObjectUUID);
                        }
                    }
                }
            }
            used.Remove(UUID.Empty);
            assetReferences.RemoveAll(ar => !used.Contains(ar.UUID));
        }





        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable data by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(string varName)
        {
            variables ??= new List<VariableData>();
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
                return variables.FirstOrDefault(v => v.name == varName);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Get variable by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(UUID uuid)
        {
            variables ??= new List<VariableData>();
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
            else return variables.FirstOrDefault(v => v.UUID == uuid);
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
            string wanted = "New " + node.GetType().Name;
            return GenerateNewNodeName(wanted);
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// Generate new name for new node
        /// </summary> 
        /// <returns></returns>
        public string GenerateNewNodeName(string wanted)
        {
            if (!nodes.Any(n => n.name == wanted))
            {
                return wanted;
            }
            int i = 2;
            while (true)
            {
                var newName = wanted + " " + i;
                if (!nodes.Any(n => n.name == newName))
                {
                    return newName;
                }
                i++;
            }
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
            VariableData vData = new(name: GenerateNewVariableName("New" + variableType), variableType: variableType);
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
            if (recordUndo) Undo.RecordObject(this, $"Remove node {node.name} from {name}");
            nodes.Remove(node);
            // clear head
            if (node == Head) headNodeUUID = UUID.Empty;
            // normal clear
            else
            {
                var parent = GetNode(node.parent);
                if (parent is IListFlow flow)
                {
                    flow.Remove(node);
                }
                else
                {
                    var nodeRef = parent.GetChildrenReference()?.FirstOrDefault(r => r?.UUID == node.uuid);
                    (nodeRef as INodeReference)?.Set(null);
                }
            }
            serializedObject.Update();
        }

        /// <summary>
        /// EDITOR ONLY <br/>
        /// remove the node and the subtree under the node from the tree
        /// </summary>
        /// <param name="node"></param>
        public void RemoveSubTree(TreeNode node, bool recordUndo = true)
        {
            if (recordUndo) Undo.RecordObject(this, $"Remove node {node.name} from {name}");
            // recursive delete
            foreach (var item in node.GetChildrenReference())
            {
                var child = GetNode(item);
                if (child != null) RemoveSubTree(child);
            }
            Remove(node, false);
        }

        public void ReLink()
        {
            RegenerateTable();
            foreach (var item in AllNodes)
            {
                var child = item.GetChildrenReference();
                foreach (var childNodeRef in child)
                {
                    if (Dictionary.TryGetValue(childNodeRef.UUID, out var childNode))
                    {
                        if (childNode != null && GetNode(childNode.parent) == null) childNode.parent = item;
                    }
                }
            }
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