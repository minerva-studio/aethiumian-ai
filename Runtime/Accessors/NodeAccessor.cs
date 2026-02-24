using Amlos.AI.Nodes;
using Amlos.AI.References;
using Amlos.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Amlos.AI.Accessors
{
    /// <summary>
    /// Provides metadata and cached access to node reference and variable fields.
    /// </summary>
    public abstract class NodeAccessor
    {
        /// <summary>
        /// Gets the runtime node type this accessor targets.
        /// </summary>
        /// <returns>The node <see cref="Type"/> that the accessor supports.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public abstract Type NodeType { get; }

        /// <summary>
        /// Gets cached accessors for node reference fields.
        /// </summary>
        /// <returns>A read-only list of accessors for node reference fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public abstract IReadOnlyList<NodeReferenceAccessor> NodeReferences { get; }

        /// <summary>
        /// Gets cached accessors for node reference list fields.
        /// </summary>
        /// <returns>A read-only list of accessors for node reference list fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public abstract IReadOnlyList<NodeReferenceCollectionAccessor> NodeReferenceCollections { get; }

        /// <summary>
        /// Gets cached accessors for variable fields.
        /// </summary>
        /// <returns>A read-only list of accessors for variable fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public abstract IReadOnlyList<VariableAccessor> Variables { get; }

        /// <summary>
        /// Gets cached accessors for variable list fields.
        /// </summary>
        /// <returns>A read-only list of accessors for variable list fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public abstract IReadOnlyList<VariableCollectionAccessor> VariableCollections { get; }

        public IEnumerable<INodeReference> GetNodeReferences(TreeNode treeNode)
        {
            foreach (var accessor in NodeReferences)
            {
                yield return accessor.Get(treeNode);
            }
            foreach (var collectionAccessor in NodeReferenceCollections)
            {
                var collection = collectionAccessor.Get(treeNode);
                foreach (var item in collection)
                {
                    if (item is INodeReference reference)
                    {
                        yield return reference;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Provides cached field access for a specific node type.
    /// </summary>
    /// <typeparam name="T">The node type to access.</typeparam>
    public sealed class NodeAccessor<T> : NodeAccessor where T : TreeNode
    {
        private readonly NodeReferenceAccessor[] nodeReferenceAccessors;
        private readonly NodeReferenceCollectionAccessor[] nodeReferenceCollectionAccessors;
        private readonly VariableAccessor[] variableAccessors;
        private readonly VariableCollectionAccessor[] variableCollectionAccessors;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeAccessor{T}"/> class.
        /// </summary>
        /// <param name="nodeReferenceAccessors">Cached accessors for node reference fields.</param>
        /// <param name="nodeReferenceCollectionAccessors">Cached accessors for node reference list fields.</param>
        /// <param name="variableAccessors">Cached accessors for variable fields.</param>
        /// <param name="variableCollectionAccessors">Cached accessors for variable list fields.</param>
        /// <returns>A constructed <see cref="NodeAccessor{T}"/> instance.</returns>
        /// <remarks>Exceptions: none.</remarks>
        internal NodeAccessor(
            NodeReferenceAccessor[] nodeReferenceAccessors,
            NodeReferenceCollectionAccessor[] nodeReferenceCollectionAccessors,
            VariableAccessor[] variableAccessors,
            VariableCollectionAccessor[] variableCollectionAccessors)
        {
            this.nodeReferenceAccessors = nodeReferenceAccessors;
            this.nodeReferenceCollectionAccessors = nodeReferenceCollectionAccessors;
            this.variableAccessors = variableAccessors;
            this.variableCollectionAccessors = variableCollectionAccessors;
        }

        /// <summary>
        /// Gets the runtime node type this accessor targets.
        /// </summary>
        /// <returns>The node <see cref="Type"/> that the accessor supports.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public override Type NodeType => typeof(T);

        /// <summary>
        /// Gets cached accessors for node reference fields.
        /// </summary>
        /// <returns>A read-only list of accessors for node reference fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public override IReadOnlyList<NodeReferenceAccessor> NodeReferences => nodeReferenceAccessors;

        /// <summary>
        /// Gets cached accessors for node reference list fields.
        /// </summary>
        /// <returns>A read-only list of accessors for node reference list fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public override IReadOnlyList<NodeReferenceCollectionAccessor> NodeReferenceCollections => nodeReferenceCollectionAccessors;

        /// <summary>
        /// Gets cached accessors for variable fields.
        /// </summary>
        /// <returns>A read-only list of accessors for variable fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public override IReadOnlyList<VariableAccessor> Variables => variableAccessors;

        /// <summary>
        /// Gets cached accessors for variable list fields.
        /// </summary>
        /// <returns>A read-only list of accessors for variable list fields.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public override IReadOnlyList<VariableCollectionAccessor> VariableCollections => variableCollectionAccessors;

        /// <summary>
        /// Builds a cached accessor for the current node type.
        /// </summary>
        /// <returns>A cached <see cref="NodeAccessor{T}"/> instance.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public static NodeAccessor<T> Create()
        {
            return NodeAccessorBuilder<T>.Create();
        }
    }

    /// <summary>
    /// Provides access to a single node reference field.
    /// </summary>
    public readonly struct NodeReferenceAccessor
    {
        private readonly Func<TreeNode, INodeReference> getter;
        private readonly Action<TreeNode, INodeReference> setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeReferenceAccessor"/> struct.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="fieldType">The runtime field type.</param>
        /// <param name="getter">The cached getter delegate.</param>
        /// <param name="setter">The cached setter delegate.</param>
        /// <returns>A configured <see cref="NodeReferenceAccessor"/> struct.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public NodeReferenceAccessor(
            string name,
            Type fieldType,
            Func<TreeNode, INodeReference> getter,
            Action<TreeNode, INodeReference> setter)
        {
            Name = name;
            FieldType = fieldType;
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        /// <returns>The field name as a <see cref="string"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the runtime field type.
        /// </summary>
        /// <returns>The <see cref="Type"/> of the field.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public Type FieldType { get; }

        /// <summary>
        /// Gets the node reference from the provided node.
        /// </summary>
        /// <param name="node">The node instance to read from.</param>
        /// <returns>The field value as an <see cref="INodeReference"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public INodeReference Get(TreeNode node)
        {
            return getter(node);
        }

        /// <summary>
        /// Sets the node reference on the provided node.
        /// </summary>
        /// <param name="node">The node instance to write to.</param>
        /// <param name="reference">The node reference to assign.</param>
        /// <returns>None.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public void Set(TreeNode node, INodeReference reference)
        {
            setter(node, reference);
        }
    }

    /// <summary>
    /// Provides access to a node reference collection field.
    /// </summary>
    public readonly struct NodeReferenceCollectionAccessor
    {
        private readonly Func<TreeNode, IList> getter;
        private readonly Action<TreeNode, IList> setter;

        /// <summary>
        /// Initializes a new instance of the <see cref="NodeReferenceCollectionAccessor"/> struct.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="collectionType">The runtime collection type.</param>
        /// <param name="elementType">The element type of the collection.</param>
        /// <param name="getter">The cached getter delegate.</param>
        /// <param name="setter">The cached setter delegate.</param>
        /// <returns>A configured <see cref="NodeReferenceCollectionAccessor"/> struct.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public NodeReferenceCollectionAccessor(
            string name,
            Type collectionType,
            Type elementType,
            Func<TreeNode, IList> getter,
            Action<TreeNode, IList> setter)
        {
            Name = name;
            CollectionType = collectionType;
            ElementType = elementType;
            this.getter = getter;
            this.setter = setter;
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        /// <returns>The field name as a <see cref="string"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the runtime collection type.
        /// </summary>
        /// <returns>The collection <see cref="Type"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public Type CollectionType { get; }

        /// <summary>
        /// Gets the element type for the collection.
        /// </summary>
        /// <returns>The element <see cref="Type"/> for the collection.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public Type ElementType { get; }

        /// <summary>
        /// Gets the node reference collection from the provided node.
        /// </summary>
        /// <param name="node">The node instance to read from.</param>
        /// <returns>The field value as an <see cref="IList"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public IList Get(TreeNode node)
        {
            return getter(node);
        }

        /// <summary>
        /// Sets the node reference collection on the provided node.
        /// </summary>
        /// <param name="node">The node instance to write to.</param>
        /// <param name="collection">The collection to assign.</param>
        /// <returns>None.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public void Set(TreeNode node, IList collection)
        {
            setter(node, collection);
        }
    }

    /// <summary>
    /// Provides access to a single variable field.
    /// </summary>
    public readonly struct VariableAccessor
    {
        private readonly Func<TreeNode, VariableBase> getter;

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableAccessor"/> struct.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="fieldType">The runtime field type.</param>
        /// <param name="getter">The cached getter delegate.</param>
        /// <returns>A configured <see cref="VariableAccessor"/> struct.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public VariableAccessor(string name, Type fieldType, Func<TreeNode, VariableBase> getter)
        {
            Name = name;
            FieldType = fieldType;
            this.getter = getter;
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        /// <returns>The field name as a <see cref="string"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the runtime field type.
        /// </summary>
        /// <returns>The <see cref="Type"/> of the field.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public Type FieldType { get; }

        /// <summary>
        /// Gets the variable from the provided node.
        /// </summary>
        /// <param name="node">The node instance to read from.</param>
        /// <returns>The field value as a <see cref="VariableBase"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public VariableBase Get(TreeNode node)
        {
            return getter(node);
        }
    }

    /// <summary>
    /// Provides access to a variable collection field.
    /// </summary>
    public readonly struct VariableCollectionAccessor
    {
        private readonly Func<TreeNode, IList> getter;

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableCollectionAccessor"/> struct.
        /// </summary>
        /// <param name="name">The field name.</param>
        /// <param name="elementType">The element type of the collection.</param>
        /// <param name="getter">The cached getter delegate.</param>
        /// <returns>A configured <see cref="VariableCollectionAccessor"/> struct.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public VariableCollectionAccessor(string name, Type elementType, Func<TreeNode, IList> getter)
        {
            Name = name;
            ElementType = elementType;
            this.getter = getter;
        }

        /// <summary>
        /// Gets the field name.
        /// </summary>
        /// <returns>The field name as a <see cref="string"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public string Name { get; }

        /// <summary>
        /// Gets the element type for the collection.
        /// </summary>
        /// <returns>The element <see cref="Type"/> for the collection.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public Type ElementType { get; }

        /// <summary>
        /// Gets the variable collection from the provided node.
        /// </summary>
        /// <param name="node">The node instance to read from.</param>
        /// <returns>The field value as an <see cref="IList"/>.</returns>
        /// <remarks>Exceptions: none.</remarks>
        public IList Get(TreeNode node)
        {
            return getter(node);
        }
    }
}
