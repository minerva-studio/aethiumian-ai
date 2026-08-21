using Aethiumian.AI.Accessors;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;

namespace Aethiumian.AI
{
    /// <summary>
    /// Resolves all runtime bindings reported by a node descriptor.
    /// </summary>
    internal sealed class NodeMemberBindingVisitor : NodeMemberVisitor
    {
        private readonly BehaviourTree behaviourTree;

        /// <summary>Initializes a binding visitor for one runtime BehaviourTree.</summary>
        /// <param name="behaviourTree">The runtime tree that owns the resolution tables.</param>
        public NodeMemberBindingVisitor(BehaviourTree behaviourTree)
        {
            this.behaviourTree = behaviourTree;
        }

        /// <summary>Resolves both normal and raw node references against the owning tree.</summary>
        protected override void OnNodeReference(string path, INodeReference reference)
        {
            behaviourTree.GetNode(reference);
        }

        /// <inheritdoc />
        protected override void OnVariableBinding(string path, IVariableBinding binding)
        {
            behaviourTree.ResolveVariableBinding(binding);
        }
    }
}
