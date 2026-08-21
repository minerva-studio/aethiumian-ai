using Aethiumian.AI.Nodes;
using Aethiumian.AI.References;
using Aethiumian.AI.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aethiumian.AI.Accessors
{
    /// <summary>Describes editor-mutable node-reference members for one node type.</summary>
    public interface INodeReferenceStructure
    {
        /// <summary>Gets the editable reference slots owned by a node.</summary>
        IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner);
    }

    /// <summary>
    /// Adds indexed mutation operations to a node-reference collection descriptor.
    /// </summary>
    public interface IIndexedNodeReferenceListSlot : INodeReferenceListSlot
    {
        /// <summary>Sets the target of an existing entry.</summary>
        /// <param name="index">The entry index.</param>
        /// <param name="treeNode">The new target, or null to clear it.</param>
        void Set(int index, TreeNode treeNode);

        /// <summary>Removes one entry by index.</summary>
        /// <param name="index">The entry index.</param>
        void RemoveAt(int index);

        /// <summary>Moves one entry within the collection.</summary>
        /// <param name="sourceIndex">The current index.</param>
        /// <param name="destinationIndex">The destination index.</param>
        void Move(int sourceIndex, int destinationIndex);
    }

    /// <summary>
    /// Supplies the finite set of built-in editable node-reference collections.
    /// </summary>
    public static class NodeReferenceStructureProvider
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, INodeReferenceStructure> structures = new();

        /// <summary>Registers an editor reference structure for a node type.</summary>
        /// <param name="nodeType">The node type described by the structure.</param>
        /// <param name="structure">The structure implementation.</param>
        public static void Register(Type nodeType, INodeReferenceStructure structure)
        {
            if (nodeType == null) throw new ArgumentNullException(nameof(nodeType));
            if (structure == null) throw new ArgumentNullException(nameof(structure));
            structures[nodeType] = structure;
        }

        /// <summary>Gets all generated scalar and manually described collection slots.</summary>
        /// <param name="owner">The node whose reference structure should be described.</param>
        /// <returns>The editable reference slots.</returns>
        public static IReadOnlyList<INodeReferenceSlot> GetSlots(TreeNode owner)
        {
            if (owner == null)
            {
                return Array.Empty<INodeReferenceSlot>();
            }

            List<INodeReferenceSlot> slots = new();
            if (structures.TryGetValue(owner.GetType(), out INodeReferenceStructure structure))
            {
                slots.AddRange(structure.GetSlots(owner));
            }

            slots.AddRange(GetBuiltInListSlots(owner));
            return slots.GroupBy(slot => slot.Name).Select(group => group.First()).ToList();
        }

        /// <summary>Gets manually described reference-list slots for one built-in node.</summary>
        /// <param name="owner">The node whose collections should be described.</param>
        /// <returns>The editable collection slots.</returns>
        public static IReadOnlyList<INodeReferenceListSlot> GetListSlots(TreeNode owner)
        {
            if (owner == null)
            {
                return Array.Empty<INodeReferenceListSlot>();
            }

            List<INodeReferenceListSlot> slots = new();
            if (structures.TryGetValue(owner.GetType(), out INodeReferenceStructure structure))
            {
                slots.AddRange(structure.GetSlots(owner).OfType<INodeReferenceListSlot>());
            }

            slots.AddRange(GetBuiltInListSlots(owner));
            return slots.GroupBy(slot => slot.Name).Select(group => group.First()).ToList();
        }

        private static IReadOnlyList<INodeReferenceListSlot> GetBuiltInListSlots(TreeNode owner)
        {
            return owner switch
            {
                Aggregate node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (NodeReference[])value, CreateNodeReference) },
                Sequence node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (NodeReference[])value, CreateNodeReference) },
                Decision node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (NodeReference[])value, CreateNodeReference) },
                Parallel node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (NodeReference[])value, CreateNodeReference) },
                Loop node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (NodeReference[])value, CreateNodeReference) },
                Probability node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (Probability.EventWeight[])value, CreateProbabilityEventWeight) },
                PseudoProbability node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.Array("events", node, () => node.events, value => node.events = (PseudoProbability.EventWeight[])value, CreatePseudoProbabilityEventWeight) },
                ServiceHostNode node => new List<INodeReferenceListSlot> { NodeReferenceListSlot.List("services", node, () => node.services, value => node.services = (List<NodeReference>)value), },
                _ => new List<INodeReferenceListSlot>(),
            };
        }

        /// <summary>Finds a direct scalar or indexed collection reference by its member path.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="path">A direct field name, optionally followed by an index.</param>
        /// <param name="reference">The discovered reference.</param>
        /// <returns>True when the path resolves to a reference.</returns>
        public static bool TryGetReference(TreeNode owner, string path, out INodeReference reference)
        {
            reference = null;
            if (!TryParseDirectPath(path, out string name, out int index))
            {
                return false;
            }

            foreach (INodeReferenceSlot slot in GetSlots(owner))
            {
                if (slot.Name != name)
                {
                    continue;
                }

                if (index < 0 && slot is INodeReferenceSingleSlot single)
                {
                    reference = single.GetReference();
                    return true;
                }

                if (index >= 0 && slot is INodeReferenceListSlot list && index < list.Count)
                {
                    reference = list.GetReference(index);
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>Sets a direct scalar or indexed collection reference to a node.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="path">A direct field name, optionally followed by an index.</param>
        /// <param name="treeNode">The new target, or null to clear it.</param>
        /// <returns>True when the path was writable.</returns>
        public static bool TrySetReference(TreeNode owner, string path, TreeNode treeNode)
        {
            if (!TryParseDirectPath(path, out string name, out int index))
            {
                return false;
            }

            foreach (INodeReferenceSlot slot in GetSlots(owner))
            {
                if (slot.Name != name)
                {
                    continue;
                }

                if (index < 0 && slot is INodeReferenceSingleSlot single)
                {
                    single.Set(treeNode);
                    return true;
                }

                if (index >= 0 && slot is IIndexedNodeReferenceListSlot list && index < list.Count)
                {
                    list.Set(index, treeNode);
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>Restores an authored UUID without resolving its runtime node.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="path">A direct field name, optionally followed by an index.</param>
        /// <param name="uuid">The authored UUID to restore.</param>
        /// <returns>True when the path was writable.</returns>
        public static bool TrySetReferenceUuid(TreeNode owner, string path, UUID uuid)
        {
            if (!TryParseDirectPath(path, out string name, out int index)) return false;

            foreach (INodeReferenceSlot slot in GetSlots(owner))
            {
                if (slot.Name != name) continue;

                INodeReference reference = null;
                if (index < 0 && slot is INodeReferenceSingleSlot single)
                {
                    reference = single.GetReference();
                    if (reference == null)
                    {
                        single.Set(null);
                        reference = single.GetReference();
                    }
                }
                else if (index >= 0 && slot is INodeReferenceListSlot list && index < list.Count)
                {
                    reference = list.GetReference(index);
                }

                if (reference == null) return false;
                reference.UUID = uuid;
                reference.Node = null;
                return true;
            }

            return false;
        }

        /// <summary>Inserts a target into a direct reference collection.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="name">The collection field name.</param>
        /// <param name="index">The insertion index.</param>
        /// <param name="treeNode">The new target, or null to clear it.</param>
        /// <returns>True when the collection was writable.</returns>
        public static bool TryInsertReference(TreeNode owner, string name, int index, TreeNode treeNode)
        {
            IIndexedNodeReferenceListSlot list = GetListSlots(owner)
                .FirstOrDefault(slot => slot.Name == name) as IIndexedNodeReferenceListSlot;
            if (list == null)
            {
                return false;
            }

            list.Insert(index, treeNode);
            return true;
        }

        /// <summary>Removes one entry from a direct reference collection.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="name">The collection field name.</param>
        /// <param name="index">The entry index.</param>
        /// <returns>True when the collection was writable.</returns>
        public static bool TryRemoveReference(TreeNode owner, string name, int index)
        {
            IIndexedNodeReferenceListSlot list = GetListSlots(owner)
                .FirstOrDefault(slot => slot.Name == name) as IIndexedNodeReferenceListSlot;
            if (list == null || index < 0 || index >= list.Count)
            {
                return false;
            }

            list.RemoveAt(index);
            return true;
        }

        /// <summary>Moves one entry in a direct reference collection.</summary>
        /// <param name="owner">The owning node.</param>
        /// <param name="name">The collection field name.</param>
        /// <param name="sourceIndex">The current index.</param>
        /// <param name="destinationIndex">The destination index.</param>
        /// <returns>True when the collection was writable.</returns>
        public static bool TryMoveReference(TreeNode owner, string name, int sourceIndex, int destinationIndex)
        {
            IIndexedNodeReferenceListSlot list = GetListSlots(owner)
                .FirstOrDefault(slot => slot.Name == name) as IIndexedNodeReferenceListSlot;
            if (list == null || sourceIndex < 0 || sourceIndex >= list.Count)
            {
                return false;
            }

            list.Move(sourceIndex, Math.Clamp(destinationIndex, 0, list.Count - 1));
            return true;
        }

        private static bool TryParseDirectPath(string path, out string name, out int index)
        {
            name = null;
            index = -1;
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            int bracket = path.IndexOf('[');
            if (bracket < 0)
            {
                name = path;
                return true;
            }

            if (!path.EndsWith("]", StringComparison.Ordinal)
                || !int.TryParse(path.Substring(bracket + 1, path.Length - bracket - 2), out index))
            {
                return false;
            }

            name = path.Substring(0, bracket);
            return !string.IsNullOrEmpty(name);
        }

        private static object CreateNodeReference(TreeNode node, int index, IList source)
        {
            return new NodeReference(node?.uuid ?? UUID.Empty);
        }

        private static object CreateProbabilityEventWeight(TreeNode node, int index, IList source)
        {
            int weight = 1;
            if (source != null && index >= 0 && index < source.Count && source[index] is Probability.EventWeight existing)
            {
                weight = existing.weight;
            }

            return new Probability.EventWeight
            {
                reference = new NodeReference(node?.uuid ?? UUID.Empty),
                weight = weight,
            };
        }

        private static object CreatePseudoProbabilityEventWeight(TreeNode node, int index, IList source)
        {
            VariableField<int> weight = 1;
            if (source != null && index >= 0 && index < source.Count && source[index] is PseudoProbability.EventWeight existing && existing.weight != null)
            {
                weight = Duplicate.Value(existing.weight);
            }

            return new PseudoProbability.EventWeight
            {
                reference = new NodeReference(node?.uuid ?? UUID.Empty),
                weight = weight,
            };
        }

        private sealed class NodeReferenceListSlot : IIndexedNodeReferenceListSlot
        {
            private readonly Func<IList> getter;
            private readonly Action<IList> setter;
            private readonly Type collectionType;
            private readonly Type elementType;
            private readonly Func<TreeNode, int, IList, object> factory;

            private NodeReferenceListSlot(
                string name,
                Type collectionType,
                Type elementType,
                Func<IList> getter,
                Action<IList> setter,
                Func<TreeNode, int, IList, object> factory)
            {
                Name = name;
                this.collectionType = collectionType;
                this.elementType = elementType;
                this.getter = getter;
                this.setter = setter;
                this.factory = factory;
            }

            public string Name { get; }
            public int Count => getter()?.Count ?? 0;

            public INodeReference GetReference(int index)
            {
                IList collection = getter();
                return collection != null && index >= 0 && index < collection.Count
                    ? collection[index] as INodeReference
                    : null;
            }

            public static NodeReferenceListSlot Array<T>(
                string name,
                TreeNode owner,
                Func<T[]> getter,
                Action<object> setter,
                Func<TreeNode, int, IList, object> factory)
            {
                return new NodeReferenceListSlot(
                    name,
                    typeof(T[]),
                    typeof(T),
                    () => getter() as IList,
                    setter,
                    factory);
            }

            public static NodeReferenceListSlot List(
                string name,
                TreeNode owner,
                Func<IList> getter,
                Action<IList> setter)
            {
                return new NodeReferenceListSlot(
                    name,
                    typeof(List<NodeReference>),
                    typeof(NodeReference),
                    getter,
                    setter,
                    CreateNodeReference);
            }

            public bool Contains(TreeNode node) => IndexOf(node) >= 0;

            public void Clear()
            {
                IList collection = getter();
                if (collection == null)
                {
                    setter(CreateCollection(0));
                    return;
                }

                if (collection.IsFixedSize)
                {
                    setter(CreateCollection(0));
                }
                else
                {
                    collection.Clear();
                }
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null) return false;
                Insert(Count, treeNode);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                IList source = getter() ?? CreateCollection(0);
                int clampedIndex = Math.Max(0, Math.Min(index, source.Count));
                IList result = CreateCollection(source.Count + 1);
                for (int i = 0; i < clampedIndex; i++) result[i] = source[i];
                result[clampedIndex] = factory(treeNode, clampedIndex, source);
                for (int i = clampedIndex; i < source.Count; i++) result[i + 1] = source[i];
                setter(result);
            }

            public void Set(int index, TreeNode treeNode)
            {
                IList collection = getter();
                if (collection == null || index < 0 || index >= collection.Count)
                {
                    return;
                }

                if (collection[index] is INodeReference reference)
                {
                    reference.Set(treeNode);
                    return;
                }

                collection[index] = factory(treeNode, index, collection);
            }

            public void RemoveAt(int index)
            {
                IList source = getter();
                if (source == null || index < 0 || index >= source.Count)
                {
                    return;
                }

                IList result = CreateCollection(source.Count - 1);
                for (int sourceIndex = 0, targetIndex = 0; sourceIndex < source.Count; sourceIndex++)
                {
                    if (sourceIndex == index)
                    {
                        continue;
                    }

                    result[targetIndex++] = source[sourceIndex];
                }

                setter(result);
            }

            public void Move(int sourceIndex, int destinationIndex)
            {
                IList source = getter();
                if (source == null || source.Count < 2
                    || sourceIndex < 0 || sourceIndex >= source.Count
                    || destinationIndex < 0 || destinationIndex >= source.Count
                    || sourceIndex == destinationIndex)
                {
                    return;
                }

                object moved = source[sourceIndex];
                IList result = CreateCollection(source.Count);
                List<object> values = source.Cast<object>().ToList();
                values.RemoveAt(sourceIndex);
                values.Insert(destinationIndex, moved);
                for (int index = 0; index < values.Count; index++)
                {
                    result[index] = values[index];
                }

                setter(result);
            }

            public int IndexOf(TreeNode node)
            {
                if (node == null) return -1;
                IList collection = getter();
                if (collection == null) return -1;
                for (int i = 0; i < collection.Count; i++)
                {
                    if (collection[i] is INodeReference reference && reference.UUID == node.uuid) return i;
                }
                return -1;
            }

            public bool Remove(TreeNode node)
            {
                int index = IndexOf(node);
                if (index < 0) return false;
                IList source = getter();
                IList result = CreateCollection(source.Count - 1);
                for (int i = 0, target = 0; i < source.Count; i++)
                {
                    if (i == index) continue;
                    result[target++] = source[i];
                }
                setter(result);
                return true;
            }

            private IList CreateCollection(int count)
            {
                if (collectionType.IsArray)
                {
                    return global::System.Array.CreateInstance(elementType, count);
                }

                if (collectionType.IsInterface || collectionType.IsAbstract)
                {
                    IList collection = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
                    for (int index = 0; index < count; index++) collection.Add(null);
                    return collection;
                }

                IList result = (IList)Activator.CreateInstance(collectionType);
                for (int index = 0; index < count; index++) result.Add(null);
                return result;
            }

        }
    }
}
