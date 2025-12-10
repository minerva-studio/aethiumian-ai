using System;
using System.Runtime.Serialization;
using Amlos.AI.Nodes;

namespace Amlos.AI
{
    [Serializable]
    public class InvalidNodeException : Exception
    {
        public Type NodeType { get; }

        public InvalidNodeException() { }

        public InvalidNodeException(string message, Type nodeType = null)
            : base(AppendNodeName(message, nodeType))
        {
            NodeType = nodeType;
        }

        public InvalidNodeException(string message, Exception inner, Type nodeType = null)
            : base(AppendNodeName(message, nodeType), inner)
        {
            NodeType = nodeType;
        }

        protected InvalidNodeException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        private static string AppendNodeName(string msg, Type nodeType)
        {
            if (nodeType == null) return msg;
            return $"[{nodeType.Name}] {msg}";
        }

        public static InvalidNodeException VariableIsRequired(string varName, TreeNode node)
        {
            return new InvalidNodeException(
                $"Variable \"{varName}\" is required",
                node.GetType()
            );
        }

        // VariableBase throws InvalidOperationException instead
        // public static InvalidNodeException InvalidValue(string varName, object value, TreeNode node)
        // {
        //     return new InvalidNodeException(
        //         $"Variable \"{varName}\" has invalid value: {value}",
        //         node.GetType()
        //     );
        // }

        // public static InvalidNodeException InvalidValue(VariableType type, object value, TreeNode node)
        // {
        //     return new InvalidNodeException(
        //         $"Variable Type \"{type}\" has invalid value: {value}",
        //         node.GetType()
        //     );
        // }

        public static InvalidNodeException InvalidValue(string message, TreeNode node)
        {
            return new InvalidNodeException(
                message,
                node.GetType()
            );
        }

        public static InvalidNodeException ReferenceIsRequired(string varName, TreeNode node)
        {
            return new InvalidNodeException(
                $"Children \"{varName}\" is required",
                node.GetType()
            );
        }
    }

}