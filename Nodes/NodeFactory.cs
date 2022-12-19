using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Amlos.AI
{
    /// <summary>
    /// A factory class use to create new node in AI Editor
    /// </summary>
    public static class NodeFactory
    {
        static Assembly[] assemblies;
        static List<Type> nodeTypes;

        public static Assembly[] Assemblies => assemblies ??= AppDomain.CurrentDomain.GetAssemblies();
        public static List<Type> NodeTypes => nodeTypes ??= GetAllNodeType();

        /// <summary>
        /// Create AI by given node type
        /// </summary>
        /// <param name="nodeType"></param>
        /// <returns></returns>
        [Obsolete]
        public static TreeNode CreateNode(NodeType nodeType)
        {
            TreeNode node = null;
            switch (nodeType)
            {
                case NodeType.decision:
                    node = new Decision();
                    break;
                case NodeType.loop:
                    node = new Loop();
                    break;
                case NodeType.sequence:
                    node = new Sequence();
                    break;
                case NodeType.condition:
                    node = new Condition();
                    break;
                case NodeType.probability:
                    node = new Probability();
                    break;
                case NodeType.always:
                    node = new Always();
                    break;
                case NodeType.inverter:
                    node = new Inverter();
                    break;
                default:
                    break;
            }
            node.name = "New " + node.GetType().Name;
            return node;
        }

        public static TreeNode CreateNode(Type nodeType)
        {
            if (!nodeType.IsSubclassOf(typeof(TreeNode))) throw new ArgumentException($"Type {nodeType} is not a valid type of node");

            TreeNode node = (TreeNode)Activator.CreateInstance(nodeType);
            FillNullField(node);
            return node;
        }

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

                if (fieldType == typeof(string))
                {
                    field.SetValue(node, "");
                }
                else
                {
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
        }



        public static IEnumerable<Type> GetSubclassesOf(Type baseType, bool allowAbstractClass = false)
        {
            return NodeTypes.Where(t => t.IsSubclassOf(baseType) && (t.IsAbstract == allowAbstractClass));
        }




        private static List<Type> GetAllNodeType()
        {
            return nodeTypes = Assemblies.SelectMany(s => s.GetTypes().Where(t => t.IsSubclassOf(typeof(TreeNode)))).ToList();
        }
    }
}