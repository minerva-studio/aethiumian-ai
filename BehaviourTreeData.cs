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


    [CreateAssetMenu(fileName = "New Behaviour Tree", menuName = "Library of Meialia/Entity/Behaviour Tree")]
    public class BehaviourTreeData : ScriptableObject
    {

        [Header("Settings")]
        public bool noActionMaximumDurationLimit;
        [DisplayIf(nameof(noActionMaximumDurationLimit), false)]
        public float actionMaximumDuration = 60;
        public BehaviourTreeErrorSolution errorHandle;
        [Header("Content")]
#if UNITY_EDITOR
        public MonoScript targetScript;
        public AnimatorController animatorController;

        [HideInInspector][SerializeReference] private Graph graph = new Graph();
#endif
        public UUID headNodeUUID;
        //public List<GenericTreeNode> nodes = new List<GenericTreeNode>();
        [SerializeReference] public List<TreeNode> nodes = new List<TreeNode>();
        public List<VariableData> variables = new List<VariableData>();
        public List<AssetReferenceData> assetReferences = new List<AssetReferenceData>();


        private Dictionary<UUID, TreeNode> dictionary;
        public Dictionary<UUID, TreeNode> Dictionary { get => dictionary ??= GenerateTable(); }

        public TreeNode Head => GetNode(headNodeUUID);



        public TreeNode GetNode(UUID uUID)
        {
            return Dictionary.TryGetValue(uUID, out var value) ? value : null;
        }

        public void RegenerateTable()
        {
            dictionary = GenerateTable();
        }

        public Dictionary<UUID, TreeNode> GenerateTable()
        {
            return nodes.Where(n => null != n).ToDictionary(n => n.uuid);
        }

        public IEnumerable<TreeNode> GetNodesCopy()
        {
            return nodes.Select(gn => gn.Clone());
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

        public UnityEngine.Object GetAsset(UUID assetReferenceUUID)
        {
            assetReferences ??= new List<AssetReferenceData>();
            return assetReferences.FirstOrDefault(a => a.uuid == assetReferenceUUID)?.asset;
        }

        public VariableData GetVariable(string varName)
        {
            variables ??= new List<VariableData>();
            return variables.FirstOrDefault(v => v.name == varName);
        }
        public VariableData GetVariable(UUID assetReferenceUUID)
        {
            variables ??= new List<VariableData>();
            return variables.FirstOrDefault(v => v.uuid == assetReferenceUUID);
        }

#if UNITY_EDITOR 
        public List<TreeNode> AllNodes { get { return nodes; } }
        public Graph Graph { get => graph ??= new Graph(); set => graph = value; }

        public UUID SetAsset(UnityEngine.Object asset)
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


        public VariableData CreateNewVariable(object value)
        {
            VariableType variableType = VariableExtensions.GetType(value);
            VariableData vData = new VariableData()
            {
                defaultValue = value.ToString(),
                type = variableType,
                name = GenerateNewVariableName("new" + variableType),
            };
            variables.Add(vData);
            return vData;
        }

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
        /// remove the node from the tree
        /// </summary>
        /// <param name="node"></param>
        public void RemoveNode(TreeNode node)
        {
            Dictionary.Remove(node.uuid);
            nodes.Remove(node);
        }
#endif
    }
}