using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections.Generic;

namespace Aethiumian.AI.Accessors
{
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
