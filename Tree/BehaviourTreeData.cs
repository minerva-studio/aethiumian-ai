using Minerva.Module;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
using UnityEditor;
#endif

/// <summary>
/// Author: Wendell
/// </summary>
namespace Amlos.AI
{
    /// <summary>
    /// Data asset of the behaviour tree
    /// </summary>
    [CreateAssetMenu(fileName = "New Behaviour Tree", menuName = "Library of Meialia/Entity/Behaviour Tree")]
    public class BehaviourTreeData : ScriptableObject
    {

        [Header("Settings")]
        public bool noActionMaximumDurationLimit;
        [DisplayIf(nameof(noActionMaximumDurationLimit), false)]
        public float actionMaximumDuration = 60;
        public BehaviourTreeErrorSolution errorHandle;
        [Header("Content")]
        public UUID headNodeUUID;
        //public List<GenericTreeNode> nodes = new List<GenericTreeNode>();
        [SerializeReference] public List<TreeNode> nodes = new List<TreeNode>();
        public List<VariableData> variables = new List<VariableData>();
        public List<AssetReferenceData> assetReferences = new List<AssetReferenceData>();



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
                List<NodeReference> children = current.GetAllChildrenReference();
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
        /// Get Asset by uuid
        /// </summary>
        /// <param name="assetReferenceUUID"></param>
        /// <returns></returns>
        public UnityEngine.Object GetAsset(UUID assetReferenceUUID)
        {
            assetReferences ??= new List<AssetReferenceData>();
            return assetReferences.FirstOrDefault(a => a.uuid == assetReferenceUUID)?.asset;
        }

#if UNITY_EDITOR
        public MonoScript targetScript;
        public AnimatorController animatorController;
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

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Add asset to behaviour tree
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public UUID AddAsset(UnityEngine.Object asset)
        {
            if (asset is MonoScript)
            {
                return UUID.Empty;
            }
            assetReferences ??= new List<AssetReferenceData>();
            AssetReferenceData assetReference = assetReferences.FirstOrDefault(a => a.asset == asset);
            if (assetReference == null)
            {
                assetReference = new AssetReferenceData(asset);
                assetReferences.Add(assetReference);
            }

            return assetReference.uuid;
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
                foreach (var field in fields.Where(f => f.FieldType.IsSubclassOf(typeof(AssetReferenceBase))))
                {
                    var reference = field.GetValue(item) as AssetReferenceBase;
                    used.Add(reference.uuid);
                }
            }
            used.Remove(UUID.Empty);
            assetReferences.RemoveAll(ar => !used.Contains(ar.uuid));
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
        public VariableData GetVariable(UUID assetReferenceUUID)
        {
            variables ??= new List<VariableData>();
            return variables.FirstOrDefault(v => v.uuid == assetReferenceUUID);
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
            else
            {
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
        }


        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Create new variable
        /// </summary> 
        /// <param name="value">default value of the variable</param>
        /// <returns></returns>
        public VariableData CreateNewVariable(object value)
        {
            VariableType variableType = VariableUtility.GetType(value);
            VariableData vData = new VariableData()
            {
                defaultValue = value.ToString(),
                type = variableType,
                name = GenerateNewVariableName("new" + variableType),
            };
            variables.Add(vData);
            return vData;
        }

        /// <summary>
        /// EDITOR ONLY <br></br>
        /// Create new variable
        /// </summary> 
        /// <param name="variableType">variable type</param>
        /// <returns></returns>
        public VariableData CreateNewVariable(VariableType variableType)
        {
            VariableData vData = new VariableData()
            {
                defaultValue = "",
                type = variableType,
                name = GenerateNewVariableName("new" + variableType),
            };
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
            VariableData vData = new VariableData()
            {
                defaultValue = "",
                type = variableType,
                name = (name),
            };
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
                var child = item.GetAllChildrenReference();
                foreach (var childNodeRef in child)
                {
                    if (Dictionary.TryGetValue(childNodeRef.uuid, out var childNode))
                    {
                        if (childNode != null && GetNode(childNode.parent) == null) childNode.parent = item;
                    }
                }
            }
        }
#endif
    }
}