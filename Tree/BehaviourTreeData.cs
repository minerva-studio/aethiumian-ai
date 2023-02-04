using Minerva.Module;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Amlos.AI.Variables;
using Amlos.AI.References;
#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEditor;
#endif

namespace Amlos.AI
{
    /// <summary>
    /// Data asset of the behaviour tree
    /// <br></br>
    /// Author: Wendell 
    /// </summary>
    [CreateAssetMenu(fileName = "AI_NAME", menuName = "Library of Meialia/Entity/Behaviour Tree")]
    public class BehaviourTreeData : ScriptableObject
    {
        [Header("Settings")]
        public bool noActionMaximumDurationLimit;
        [DisplayIf(nameof(noActionMaximumDurationLimit), false)]
        public float actionMaximumDuration = 60;
        public BehaviourTreeErrorSolution errorHandle;

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
        public Object GetAsset(UUID assetReferenceUUID)
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

        /// <summary>
        /// EDITOR ONLY<br></br>
        /// Optimization UUID-TreeNode dictionary
        /// </summary>
        public Dictionary<UUID, TreeNode> Dictionary { get => dictionary ??= GenerateTable(); }

        public TreeNode Head => GetNode(headNodeUUID);
        public List<TreeNode> AllNodes { get { return nodes; } }
        public Graph Graph { get => graph ??= new Graph(); set => graph = value; }



        /// <summary>
        /// EDITOR ONLY <br></br>
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
        /// EDITOR ONLY <br></br>
        /// Regenerate the uuid-TreeNode table
        /// </summary>
        public void RegenerateTable()
        {
            dictionary = GenerateTable();
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// generate the uuid-TreeNode table
        /// </summary>
        /// <returns></returns>
        private Dictionary<UUID, TreeNode> GenerateTable()
        {
            return nodes.Where(n => null != n).ToDictionary(n => n.uuid);
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Get a node by uuid
        /// </summary>
        /// <param name="uUID"></param>
        /// <returns></returns>
        public TreeNode GetNode(UUID uUID)
        {
            return Dictionary.TryGetValue(uUID, out var value) ? value : null;
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
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
        /// EDITOR ONLY <br></br>
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




        public bool HasAsset(Object asset)
        {
            return assetReferences.Any(t => t.Asset == asset);
        }

        public void SetAssetFromVariable(Object asset, bool isFromVariable)
        {
            AssetReferenceData assetReferenceData = assetReferences.FirstOrDefault(t => t.Asset == asset);
            assetReferenceData.isFromVariable = isFromVariable;
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Add asset to behaviour tree
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public AssetReferenceData AddAsset(Object asset, bool isFromVariable = false)
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

        public int RemoveAsset(Object asset)
        {
            return assetReferences.RemoveAll(t => t.Asset == asset);
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
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
                    if (field.FieldType.IsSubclassOf(typeof(AssetReferenceBase)))
                    {
                        var reference = field.GetValue(item) as AssetReferenceBase;
                        used.Add(reference.uuid);
                    }
                    else if (field.FieldType.IsSubclassOf(typeof(VariableBase)))
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
        /// EDITOR ONLY <br></br>
        /// Get variable data by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(string varName)
        {
            variables ??= new List<VariableData>();
            return variables.FirstOrDefault(v => v.name == varName);
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Get variable by name
        /// </summary>
        /// <param name="varName"></param>
        /// <returns></returns>
        public VariableData GetVariable(UUID uuid)
        {
            variables ??= new List<VariableData>();
            return variables.FirstOrDefault(v => v.UUID == uuid);
        }


        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Generate new name for new node
        /// </summary> 
        /// <returns></returns>
        public string GenerateNewNodeName(TreeNode node)
        {
            string wanted = "New " + node.GetType().Name;
            if (!nodes.Any(n => n.name == wanted))
            {
                return wanted;
            }
            else
            {
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
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
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
        /// EDITOR ONLY <br></br>
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
        /// EDITOR ONLY <br></br>
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
        /// EDITOR ONLY <br></br>
        /// Create new variable
        /// </summary> 
        /// <param name="variableType">variable type</param>
        /// <returns></returns>
        public void AddNode(TreeNode p)
        {
            if (p is null)
            {
                return;
            }
            nodes.Add(p);
            Dictionary[p.uuid] = p;
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// remove the node from the tree
        /// </summary>
        /// <param name="node"></param>
        public void RemoveNode(TreeNode node)
        {
            Dictionary.Remove(node.uuid);
            nodes.Remove(node);
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
#endif
    }
}