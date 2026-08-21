using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Accessors
{
    /// <summary>Represents the complete path of the member currently visited.</summary>
    public readonly struct NodeMemberPath
    {
        /// <summary>Creates a parsed member path.</summary>
        public NodeMemberPath(string fullPath)
        {
            FullPath = fullPath ?? string.Empty;
            int separator = FullPath.IndexOf('.');
            string root = separator < 0 ? FullPath : FullPath.Substring(0, separator);
            int bracket = root.IndexOf('[');
            RootName = bracket < 0 ? root : root.Substring(0, bracket);
            Index = -1;
            if (bracket >= 0 && root.EndsWith("]", StringComparison.Ordinal))
            {
                if (int.TryParse(root.Substring(bracket + 1, root.Length - bracket - 2), out int index))
                {
                    Index = index;
                }
            }
        }

        /// <summary>Gets the complete nested path.</summary>
        public string FullPath { get; }

        /// <summary>Gets the root field name.</summary>
        public string RootName { get; }

        /// <summary>Gets the root collection index, or -1 for a scalar.</summary>
        public int Index { get; }

        /// <inheritdoc />
        public override string ToString() => FullPath;
    }

    /// <summary>
    /// Visits semantic node members while maintaining the full nested member path.
    /// </summary>
    public abstract class NodeMemberVisitor
    {
        private readonly List<string> path = new();

        /// <summary>Visits a node reference at the current member path.</summary>
        public void VisitNodeReference(string name, INodeReference reference)
        {
            if (reference == null)
            {
                return;
            }

            using (Push(name))
            {
                OnNodeReference(CurrentPath, reference);
            }
        }

        /// <summary>Visits a variable binding at the current member path.</summary>
        public void VisitVariableBinding(string name, IVariableBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            using (Push(name))
            {
                OnVariableBinding(CurrentPath, binding);
            }
        }

        /// <summary>Visits a custom nested member.</summary>
        public void VisitMember(string name, INodeMember member)
        {
            if (member == null)
            {
                return;
            }

            using (Push(name))
            {
                member.Visit(this);
            }
        }

        /// <summary>Visits an indexed custom nested member.</summary>
        public void VisitMember(string name, int index, INodeMember member)
        {
            if (member == null)
            {
                return;
            }

            using (Push(name + "[" + index + "]"))
            {
                member.Visit(this);
            }
        }

        /// <summary>Gets the path currently being visited.</summary>
        protected string CurrentPath => string.Join(".", path);

        /// <summary>Gets the parsed path currently being visited.</summary>
        protected NodeMemberPath CurrentMemberPath => new(CurrentPath);

        /// <summary>Handles one discovered node reference.</summary>
        protected abstract void OnNodeReference(string path, INodeReference reference);

        /// <summary>Handles one discovered variable binding.</summary>
        protected abstract void OnVariableBinding(string path, IVariableBinding binding);

        private IDisposable Push(string segment)
        {
            path.Add(string.IsNullOrEmpty(segment) ? "<unnamed>" : segment);
            return new PathScope(path);
        }

        private sealed class PathScope : IDisposable
        {
            private readonly List<string> path;

            public PathScope(List<string> path)
            {
                this.path = path;
            }

            public void Dispose()
            {
                path.RemoveAt(path.Count - 1);
            }
        }
    }

    /// <summary>
    /// Provides explicit traversal for nested node members that cannot be inferred safely by the generator.
    /// </summary>
    public interface INodeMember
    {
        /// <summary>Reports all semantic members owned by this object.</summary>
        void Visit(NodeMemberVisitor visitor);
    }
}
