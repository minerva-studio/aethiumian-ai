﻿using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// A factory class use to create new node in AI Editor
    /// </summary>
    public static class NodeFactory
    {
        //private static Assembly[] assemblies;
        //private static readonly HashSet<string> ignoredAssemblyNames = new()
        //{
        //    "Bee.BeeDriver",
        //    "ExCSS.Unity",
        //    "Mono.Security",
        //    "mscorlib",
        //    "netstandard",
        //    "Newtonsoft.Json",
        //    "nunit.framework",
        //    "ReportGeneratorMerged",
        //    "Unrelated",
        //    "SyntaxTree.VisualStudio.Unity.Bridge",
        //    "SyntaxTree.VisualStudio.Unity.Messaging",
        //};
        //public static Assembly[] UserAssemblies => assemblies ??= GetUserCreatedAssemblies();
        //private static Assembly[] GetUserCreatedAssemblies()
        //{
        //    return GetUserCreatedAssemblies().ToArray();
        //    static IEnumerable<Assembly> GetUserCreatedAssemblies()
        //    {
        //        var appDomain = AppDomain.CurrentDomain;
        //        foreach (var assembly in appDomain.GetAssemblies())
        //        {
        //            if (assembly.IsDynamic)
        //            {
        //                continue;
        //            }

        //            var assemblyName = assembly.GetName().Name;
        //            if (assemblyName.StartsWith("System") ||
        //               assemblyName.StartsWith("Unity") ||
        //               assemblyName.StartsWith("UnityEditor") ||
        //               assemblyName.StartsWith("UnityEngine") ||
        //               ignoredAssemblyNames.Contains(assemblyName))
        //            {
        //                continue;
        //            }

        //            yield return assembly;
        //        }
        //    }
        //} 


        /// <summary>
        /// Create a node by type
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static TreeNode Create(Type nodeType)
        {
            if (!nodeType.IsSubclassOf(typeof(TreeNode))) throw new ArgumentException($"Type {nodeType} is not a valid type of node");
            if (nodeType.IsAbstract) throw new ArgumentException($"Type {nodeType} is an abstract node type");

            TreeNode node = (TreeNode)Activator.CreateInstance(nodeType);
            node.uuid = UUID.NewUUID();
            FillNull(node);
            return node;
        }

        /// <summary>
        /// Fill all empty field in the node with value (if supported)
        /// </summary>
        /// <param name="node"></param>
        public static void FillNull(TreeNode node)
        {
            var type = node.GetType();
            var fields = type.GetFields();
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                var value = field.GetValue(node);
                //Null Determine
                if (!fieldType.IsClass || value is not null)
                {
                    continue;
                }
                // do not try to create an instance for unity managed object
                if (fieldType.IsSubclassOf(typeof(UnityEngine.Object)))
                {
                    continue;
                }

                if (fieldType == typeof(string))
                {
                    field.SetValue(node, "");
                    continue;
                }

                try
                {
                    field.SetValue(node, Activator.CreateInstance(fieldType));
                }
                catch (Exception)
                {
                    field.SetValue(node, default);
                    Debug.LogWarning("Field " + field.Name + " has not initialized yet. Provide this information if there are bugs");
                }
            }
        }




        public static bool CanReceiveDuplicateItem(TreeNode node)
        {
            return node is Sequence or Decision or Probability or Service;
        }






        /// <summary>
        /// Perform a deep copy of the object via serialization.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", nameof(source));
            }

            return JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
        }

        /// <summary>
        /// Get a copy of the object via serialization. (result in same uuid and name)
        /// </summary>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>A deep copy of the object.</returns>
        public static TreeNode Clone(TreeNode source, Type type)
        {
            return (TreeNode)JsonUtility.FromJson(JsonUtility.ToJson(source), type);
        }

        /// <summary>
        /// Create deep clone of the tree node, this will assign the new node with different uuid
        /// </summary>
        /// <param name="treeNode"></param>
        /// <returns></returns>
        public static TreeNode DeepClone(TreeNode treeNode)
        {
            var cloned = Clone(treeNode, treeNode.GetType());
            cloned.uuid = UUID.NewUUID();
            return cloned;
        }

#if UNITY_EDITOR 

        /// <summary>
        /// Clone the entire subtree
        /// </summary>
        /// <returns> List of node cloned and linked, root clone will be the first in the list </returns>
        public static List<TreeNode> DeepCloneSubTree(TreeNode root, BehaviourTreeData data)
        {
            Dictionary<UUID, UUID> translationTable = new Dictionary<UUID, UUID>();
            List<TreeNode> result = new();

            BuildTableSubTree(translationTable, result, root, data);
            ApplyTranslation(translationTable, result);

            return result;
        }

        /// <summary>
        /// Apply the translation of the uuid
        /// </summary>
        /// <param name="translationTable"></param>
        /// <param name="result"></param>
        private static void ApplyTranslation(Dictionary<UUID, UUID> translationTable, List<TreeNode> result)
        {
            foreach (var node in result)
            {
                if (translationTable.ContainsKey(node.parent.UUID))
                {
                    node.parent.UUID = translationTable[node.parent.UUID];
                }
                foreach (var item in node.GetChildrenReference(true))
                {
                    if (!translationTable.ContainsKey(item.UUID))
                    {
                        if (item is not RawNodeReference && item.UUID != UUID.Empty)
                            Debug.LogError($"Cannot find uuid in translation table ({item.UUID}), this is potentially an error in the subtree copy.");
                        continue;
                    }

                    item.UUID = translationTable[item.UUID];
                }
            }
        }

        /// <summary>
        /// Clone the subtree and build the translation table of the cloned tree
        /// </summary>
        /// <returns></returns>
        private static void BuildTableSubTree(Dictionary<UUID, UUID> translationTable, List<TreeNode> result, TreeNode root, BehaviourTreeData data)
        {
            var cloned = DeepClone(root);
            translationTable[root.uuid] = cloned.uuid;
            result.Add(cloned);
            var childrens = root.GetChildrenReference();

            foreach (var childRef in childrens)
            {
                TreeNode child = data.GetNode(childRef);
                if (child != null)
                    BuildTableSubTree(translationTable, result, child, data);
            }
        }

        /// <summary>
        /// Reassign the uuid of the given subtree
        /// </summary>
        /// <param name="contents"></param>
        public static void ReassignUUID(List<TreeNode> contents)
        {
            Dictionary<UUID, UUID> translationTable = new();

            foreach (var node in contents)
            {
                UUID uuid = node.uuid;
                UUID newUUID = UUID.NewUUID();

                translationTable[uuid] = newUUID;
                node.uuid = newUUID;
            }

            ApplyTranslation(translationTable, contents);
        }
#endif

        /// <summary>
        /// Copy data from src to dst
        /// </summary>
        /// <param name="dst"></param>
        /// <param name="src"></param>
        public static void Copy(TreeNode dst, TreeNode src)
        {
            var fields = dst.GetType().GetFields();
            foreach (var field in fields)
            {
                object value = field.GetValue(src);
                if (field.Name == nameof(dst.parent)) continue;
                if (field.Name == nameof(dst.uuid)) continue;
                if (field.Name == nameof(dst.name)) continue;
                if (value is NodeReference) continue;
                if (value is NodeReference[]) continue;
                if (value is List<NodeReference>) continue;

                if (value is ValueType structVal)
                {
                    value = structVal;
                }
                else if (value is ICloneable cloneable)
                {
                    value = cloneable.Clone();
                }
                else if (value is Array arr)
                {
                    var newArray = Array.CreateInstance(arr.GetType().GetElementType(), arr.Length);
                    Array.Copy(arr, newArray, newArray.Length);
                    value = newArray;
                }
                else if (value is IList list)
                {
                    var dstList = field.GetValue(dst) as IList;
                    foreach (var item in list)
                    {
                        object value1 = item;
                        if (item is ValueType v) value1 = v;
                        else if (item is ICloneable c) value1 = c.Clone();
                        dstList.Add(value1);
                    }
                }
                Debug.Log($"Copy field {field.Name}");
                field.SetValue(dst, value);
            }
        }
    }
}
