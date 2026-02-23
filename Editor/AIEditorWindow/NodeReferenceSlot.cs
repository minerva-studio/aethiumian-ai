using Amlos.AI.Accessors;
using Amlos.AI.Nodes;
using Amlos.AI.References;
using Minerva.Module;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Amlos.AI.Editor
{
    public interface INodeReferenceSlot
    {
        string Name { get; }
        bool Contains(TreeNode node);
        void Clear();
    }

    public interface INodeReferenceSingleSlot : INodeReferenceSlot
    {
        void Set(TreeNode treeNode);
    }

    public interface INodeReferenceListSlot : INodeReferenceSlot
    {
        int Count { get; }
        bool Add(TreeNode treeNode);
        void Insert(int index, TreeNode treeNode);
        int IndexOf(TreeNode treeNode);
        bool Remove(TreeNode treeNode);
    }

    public static class NodeReferenceSlotExtensions
    {
        public static List<INodeReferenceSlot> ToReferenceSlots(this TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return new List<INodeReferenceSlot>();
            }

            var slots = new List<INodeReferenceSlot>();
            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(treeNode.GetType());

            foreach (var referenceAccessor in accessor.NodeReferences)
            {
                // ignore parent
                if (referenceAccessor.Name == nameof(treeNode.parent)) continue;
                slots.Add(new AccessorSingleSlot(ToTitleCase(referenceAccessor.Name), treeNode, referenceAccessor));
            }

            foreach (var collectionAccessor in accessor.NodeReferenceCollections)
            {
                var listSlot = CreateListSlot(treeNode, collectionAccessor);
                if (listSlot != null)
                {
                    slots.Add(listSlot);
                }
            }

            return slots;
        }

        public static INodeReferenceListSlot GetListSlot(this TreeNode treeNode)
        {
            if (treeNode == null)
            {
                return null;
            }

            NodeAccessor accessor = NodeAccessorProvider.GetAccessor(treeNode.GetType());
            foreach (var collectionAccessor in accessor.NodeReferenceCollections)
            {
                var slot = CreateListSlot(treeNode, collectionAccessor);
                if (slot != null)
                {
                    return slot;
                }
            }

            return null;
        }

        public static bool DetachFrom(this TreeNode draggedNode, TreeNode oldParent)
        {
            if (oldParent == null)
            {
                return false;
            }

            var oldSlots = oldParent.ToReferenceSlots();
            for (int i = 0; i < oldSlots.Count; i++)
            {
                var slot = oldSlots[i];
                if (!slot.Contains(draggedNode))
                {
                    continue;
                }

                if (slot is INodeReferenceSingleSlot single)
                {
                    single.Clear();
                    return true;
                }

                if (slot is INodeReferenceListSlot list)
                {
                    list.Remove(draggedNode);
                    return true;
                }
            }
            return false;
        }

        private static INodeReferenceListSlot CreateListSlot(TreeNode owner, NodeReferenceCollectionAccessor collectionAccessor)
        {
            if (collectionAccessor.CollectionType.IsArray)
            {
                if (collectionAccessor.ElementType == typeof(Probability.EventWeight))
                {
                    return new ProbabilityEventWeightArraySlot(ToTitleCase(collectionAccessor.Name), owner, collectionAccessor);
                }

                if (collectionAccessor.ElementType == typeof(PseudoProbability.EventWeight))
                {
                    return new PseudoProbabilityEventWeightArraySlot(ToTitleCase(collectionAccessor.Name), owner, collectionAccessor);
                }

                return new NodeReferenceArraySlot(ToTitleCase(collectionAccessor.Name), owner, collectionAccessor);
            }

            return new NodeReferenceListSlot(ToTitleCase(collectionAccessor.Name), owner, collectionAccessor);
        }

        private static string ToTitleCase(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            if (name.Length == 1)
            {
                return char.ToUpperInvariant(name[0]).ToString();
            }

            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static INodeReference CreateReference(Type referenceType, TreeNode treeNode)
        {
            INodeReference reference = (INodeReference)Activator.CreateInstance(referenceType);
            reference.UUID = treeNode?.uuid ?? UUID.Empty;
            reference.Node = null;
            return reference;
        }

        private static IList CreateCollection(Type collectionType, Type elementType)
        {
            if (collectionType.IsInterface || collectionType.IsAbstract)
            {
                return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));
            }

            return (IList)Activator.CreateInstance(collectionType);
        }

        private sealed class AccessorSingleSlot : INodeReferenceSingleSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly NodeReferenceAccessor accessor;

            public AccessorSingleSlot(string name, TreeNode owner, NodeReferenceAccessor accessor)
            {
                this.name = name;
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => name;

            public bool Contains(TreeNode node)
            {
                if (owner == null || node == null)
                {
                    return false;
                }

                INodeReference reference = accessor.Get(owner);
                return reference != null && reference.UUID == node.UUID;
            }

            public void Clear()
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, CreateReference(accessor.FieldType, null));
            }

            public void Set(TreeNode treeNode)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, CreateReference(accessor.FieldType, treeNode));
            }
        }

        private sealed class NodeReferenceArraySlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly NodeReferenceCollectionAccessor accessor;

            public NodeReferenceArraySlot(string name, TreeNode owner, NodeReferenceCollectionAccessor accessor)
            {
                this.name = name;
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => name;

            private Array GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as Array ?? Array.CreateInstance(accessor.ElementType, 0);
            }

            private void SetArray(Array arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, (IList)arr ?? Array.CreateInstance(accessor.ElementType, 0));
            }

            public int Count => GetArray().Length;

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.CreateInstance(accessor.ElementType, 0));
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length + 1);
                Array.Copy(arr, newArr, arr.Length);
                newArr.SetValue(CreateReference(accessor.ElementType, treeNode), newArr.Length - 1);
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length + 1);
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr.SetValue(CreateReference(accessor.ElementType, treeNode), clampedIndex);
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr.GetValue(i) is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.CreateInstance(accessor.ElementType, 0));
                    return true;
                }

                Array newArr = Array.CreateInstance(accessor.ElementType, arr.Length - 1);
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }

        private sealed class NodeReferenceListSlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly NodeReferenceCollectionAccessor accessor;

            public NodeReferenceListSlot(string name, TreeNode owner, NodeReferenceCollectionAccessor accessor)
            {
                this.name = name;
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => name;

            private IList GetList()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner);
            }

            private IList EnsureList()
            {
                if (owner == null)
                {
                    return null;
                }

                IList list = accessor.Get(owner);
                if (list == null)
                {
                    list = CreateCollection(accessor.CollectionType, accessor.ElementType);
                    accessor.Set(owner, list);
                }

                return list;
            }

            public int Count => GetList()?.Count ?? 0;

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                IList list = GetList();
                if (list == null)
                {
                    return false;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                IList list = EnsureList();
                list?.Clear();
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                IList list = EnsureList();
                if (list == null)
                {
                    return false;
                }

                list.Add(CreateReference(accessor.ElementType, treeNode));
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                IList list = EnsureList();
                if (list == null)
                {
                    return;
                }

                int clampedIndex = index < 0 || index > list.Count ? list.Count : index;
                list.Insert(clampedIndex, CreateReference(accessor.ElementType, treeNode));
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                IList list = GetList();
                if (list == null)
                {
                    return -1;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                IList list = GetList();
                if (list == null)
                {
                    return false;
                }

                int index = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is INodeReference reference && reference.UUID == treeNode.UUID)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    return false;
                }

                list.RemoveAt(index);
                return true;
            }
        }

        private sealed class ProbabilityEventWeightArraySlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly NodeReferenceCollectionAccessor accessor;

            public ProbabilityEventWeightArraySlot(string name, TreeNode owner, NodeReferenceCollectionAccessor accessor)
            {
                this.name = name;
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => name;

            private Probability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as Probability.EventWeight[] ?? Array.Empty<Probability.EventWeight>();
            }

            private void SetArray(Probability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, arr ?? Array.Empty<Probability.EventWeight>());
            }

            public int Count => GetArray().Length;

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.Empty<Probability.EventWeight>());
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                var newArr = new Probability.EventWeight[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[^1] = new Probability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = 1 };
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                int weight = 1;
                if (arr.Length > clampedIndex && clampedIndex > 0 && arr[clampedIndex] != null)
                {
                    weight = arr[clampedIndex].weight;
                }

                var newArr = new Probability.EventWeight[arr.Length + 1];
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr[clampedIndex] = new Probability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = weight };
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.Empty<Probability.EventWeight>());
                    return true;
                }

                var newArr = new Probability.EventWeight[arr.Length - 1];
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }

        private sealed class PseudoProbabilityEventWeightArraySlot : INodeReferenceListSlot
        {
            private readonly string name;
            private readonly TreeNode owner;
            private readonly NodeReferenceCollectionAccessor accessor;

            public PseudoProbabilityEventWeightArraySlot(string name, TreeNode owner, NodeReferenceCollectionAccessor accessor)
            {
                this.name = name;
                this.owner = owner;
                this.accessor = accessor;
            }

            public string Name => name;

            private PseudoProbability.EventWeight[] GetArray()
            {
                if (owner == null)
                {
                    return null;
                }

                return accessor.Get(owner) as PseudoProbability.EventWeight[] ?? Array.Empty<PseudoProbability.EventWeight>();
            }

            private void SetArray(PseudoProbability.EventWeight[] arr)
            {
                if (owner == null)
                {
                    return;
                }

                accessor.Set(owner, arr ?? Array.Empty<PseudoProbability.EventWeight>());
            }

            public int Count => GetArray().Length;

            public bool Contains(TreeNode node)
            {
                if (node == null)
                {
                    return false;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == node.UUID)
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Clear()
            {
                SetArray(Array.Empty<PseudoProbability.EventWeight>());
            }

            public bool Add(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                var newArr = new PseudoProbability.EventWeight[arr.Length + 1];
                Array.Copy(arr, newArr, arr.Length);
                newArr[^1] = new PseudoProbability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = 1 };
                SetArray(newArr);
                return true;
            }

            public void Insert(int index, TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return;
                }

                var arr = GetArray();
                int clampedIndex = index < 0 || index > arr.Length ? arr.Length : index;

                int weight = 1;
                if (arr.Length > clampedIndex && clampedIndex > 0 && arr[clampedIndex] != null)
                {
                    weight = arr[clampedIndex].weight;
                }

                var newArr = new PseudoProbability.EventWeight[arr.Length + 1];
                if (clampedIndex > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, clampedIndex);
                }
                newArr[clampedIndex] = new PseudoProbability.EventWeight() { reference = new NodeReference(treeNode.UUID), weight = weight };
                if (clampedIndex < arr.Length)
                {
                    Array.Copy(arr, clampedIndex, newArr, clampedIndex + 1, arr.Length - clampedIndex);
                }

                SetArray(newArr);
            }

            public int IndexOf(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return -1;
                }

                var arr = GetArray();
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Remove(TreeNode treeNode)
            {
                if (treeNode == null)
                {
                    return false;
                }

                var arr = GetArray();
                int idx = -1;
                for (int i = 0; i < arr.Length; i++)
                {
                    if (arr[i]?.reference.UUID == treeNode.UUID)
                    {
                        idx = i;
                        break;
                    }
                }

                if (idx < 0)
                {
                    return false;
                }

                if (arr.Length == 1)
                {
                    SetArray(Array.Empty<PseudoProbability.EventWeight>());
                    return true;
                }

                var newArr = new PseudoProbability.EventWeight[arr.Length - 1];
                if (idx > 0)
                {
                    Array.Copy(arr, 0, newArr, 0, idx);
                }
                if (idx < arr.Length - 1)
                {
                    Array.Copy(arr, idx + 1, newArr, idx, arr.Length - idx - 1);
                }

                SetArray(newArr);
                return true;
            }
        }
    }
}
