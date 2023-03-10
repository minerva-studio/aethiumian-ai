using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI.Nodes
{
    /// <summary>
    /// A factory class use to create new node in AI Editor
    /// </summary>
    public static class NodeFactory
    {
        static Assembly[] assemblies;
        static List<Type> nodeTypes;


        static NodeFactory()
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
            nodeTypes = GetAllNodeType();
        }



        /// <summary>
        /// Create a node by type
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static TreeNode CreateNode(Type nodeType)
        {
            if (!nodeType.IsSubclassOf(typeof(TreeNode))) throw new ArgumentException($"Type {nodeType} is not a valid type of node");
            if (nodeType.IsAbstract) throw new ArgumentException($"Type {nodeType} is an abstract node type");

            TreeNode node = (TreeNode)Activator.CreateInstance(nodeType);
            FillNullField(node);
            return node;
        }

        /// <summary>
        /// Fill all empty field in the node with value (if supported)
        /// </summary>
        /// <param name="node"></param>
        public static void FillNullField(TreeNode node)
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



        public static IEnumerable<Type> GetSubclassesOf(Type baseType, bool allowAbstractClass = false)
        {
            return nodeTypes.Where(t => t.IsSubclassOf(baseType) && (t.IsAbstract == allowAbstractClass));
        }




        private static List<Type> GetAllNodeType()
        {
            return assemblies.SelectMany(s => s.GetTypes().Where(t => t.IsSubclassOf(typeof(TreeNode)))).ToList();
        }
    }
}